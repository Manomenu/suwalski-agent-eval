using System.Text.Json;
using Dapper;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Checks;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Tests.Runs;

public sealed class RunStoreTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private RunStore Store() => new(database.Pool);

    private Task<SeededAgent> NewAgentAsync(double threshold = 1.0) =>
        SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund", threshold)]
        );

    private static Task<Guid> EnqueueAsync(RunStore store, SeededAgent agent, string? tag = null) =>
        store.EnqueueAsync(agent.Id, agent.VersionId, tag, agent.EndpointUrl, cancellationToken: Cancellation);

    [Fact]
    public async Task An_enqueued_run_is_queued_with_no_results()
    {
        var store = Store();
        var agent = await NewAgentAsync();

        var id = await EnqueueAsync(store, agent, "regression");

        var run = await store.GetAsync(id, Cancellation);
        Assert.NotNull(run);
        Assert.Equal(
            (agent.Name, agent.VersionId, "regression", "http://agent.test"),
            (run.AgentName, run.AgentVersionId, run.Tag, run.EndpointUrl)
        );
        Assert.Equal(RunStatus.Queued, run.State);
        Assert.Equal((0, 0, 0, 0), (run.SamplesTotal, run.SamplesDone, run.ChecksPassed, run.ChecksFailed));
        Assert.Null(run.StartedAt);
        Assert.Null(await store.GetAsync(Guid.NewGuid(), Cancellation));
    }

    [Fact]
    public async Task Claiming_takes_the_oldest_queued_run_first_and_marks_it_running()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var first = await EnqueueAsync(store, agent);
        var second = await EnqueueAsync(store, agent);

        var claimedFirst = await store.ClaimNextAsync(agent.Id, Cancellation);
        var claimedSecond = await store.ClaimNextAsync(agent.Id, Cancellation);
        var nothing = await store.ClaimNextAsync(agent.Id, Cancellation);

        Assert.Equal(first, claimedFirst?.Id);
        Assert.Equal(RunStatus.Running, claimedFirst?.State);
        Assert.NotNull(claimedFirst?.StartedAt);
        Assert.Equal(second, claimedSecond?.Id);
        Assert.Null(nothing);
    }

    [Fact]
    public async Task Concurrent_claims_never_give_the_same_run_twice()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var queued = new HashSet<Guid>();
        for (var i = 0; i < 6; i++)
        {
            queued.Add(await EnqueueAsync(store, agent));
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => store.ClaimNextAsync(agent.Id, Cancellation)));

        List<Guid> claimed = [.. claims.Where(run => run is not null).Select(run => run?.Id ?? Guid.Empty)];
        Assert.Equal(queued.Count, claimed.Count);
        Assert.Equal(queued, [.. claimed]);
    }

    [Fact]
    public async Task Claiming_for_an_agent_leaves_the_runs_of_others_queued()
    {
        var store = Store();
        var mine = await NewAgentAsync();
        var other = await NewAgentAsync();
        var othersRun = await EnqueueAsync(store, other);

        Assert.Null(await store.ClaimNextAsync(mine.Id, Cancellation));
        Assert.Equal(RunStatus.Queued, (await store.GetAsync(othersRun, Cancellation))?.State);
    }

    [Fact]
    public async Task A_stored_sample_with_its_verdicts_shows_in_the_counts_and_the_results()
    {
        var store = Store();
        var agent = await NewAgentAsync(threshold: 0.8);
        var id = await EnqueueAsync(store, agent);
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);
        var check = Assert.Single(await new CheckStore(database.Pool).ListAsync(agent.Id, cancellationToken: Cancellation));

        await store.AddSampleAsync(
            id,
            new NewSample(
                scenario!.Id,
                scenario.Revision,
                1,
                SampleStatus.Done,
                /*lang=json,strict*/
                """[{"kind":"AssistantMessage","text":"hi"}]""",
                null,
                1234,
                ["tool 'cancel_order'"],
                [new NewCheckResult(check.Id, check.Revision, false, "[]", "refund_order was not called.")]
            ),
            Cancellation
        );

        var run = await store.GetAsync(id, Cancellation);
        Assert.Equal((1, 0, 1), (run?.SamplesDone, run?.ChecksPassed, run?.ChecksFailed));
        var sample = Assert.Single(await store.ListScenarioResultsAsync(id, Cancellation));
        Assert.Equal(("refund", scenario.Revision, 1, "Done"), (sample.ScenarioKey, sample.ScenarioRevision, sample.Sample, sample.Status));
        Assert.Contains("hi", sample.Output, StringComparison.Ordinal);
        Assert.Equal(1234, sample.DurationMs);
        Assert.Equal( /*lang=json,strict*/
            """["tool 'cancel_order'"]""",
            sample.Undeclared
        );
        var verdict = Assert.Single(await store.ListCheckResultsAsync(id, Cancellation));
        Assert.Equal(
            ("refund-called", 0.8, false, "refund_order was not called."),
            (verdict.CheckKey, verdict.PassThreshold, verdict.Passed, verdict.Message)
        );
        Assert.Equal(sample.Id, verdict.ScenarioResultId);
        Assert.Equal(1, verdict.LatestRevision);
        Assert.Contains("\"tool\": \"refund_order\"", verdict.Definition, StringComparison.Ordinal);

        // The check changes after the run: the run keeps the revision it used and its definition, the latest moves on.
        var changed = SeededAgent.RefundAfterLookup("refund-called", "refund", 0.5);
        await new CheckStore(database.Pool).UpsertAsync(
            agent.Id,
            [new CheckItem(changed.Key, changed.ScenarioKey, JsonSerializer.Serialize(changed, ContractJson.Options))],
            Cancellation
        );
        var later = Assert.Single(await store.ListCheckResultsAsync(id, Cancellation));
        Assert.Equal((1, 2, 0.8), (later.CheckRevision, later.LatestRevision, later.PassThreshold));
        Assert.Equal(verdict.Definition, later.Definition);
    }

    [Fact]
    public async Task A_sample_with_no_call_has_no_duration()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var id = await EnqueueAsync(store, agent);
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);

        await store.AddSampleAsync(
            id,
            new NewSample(scenario!.Id, scenario.Revision, 1, SampleStatus.Failed, null, "no", null, [], []),
            Cancellation
        );

        Assert.Null(Assert.Single(await store.ListScenarioResultsAsync(id, Cancellation)).DurationMs);
    }

    [Fact]
    public async Task Agent_errors_count_samples_without_an_answer_per_scenario_with_the_newest_error_worst_first()
    {
        var store = Store();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund"), SeededAgent.Scenario("chat"), SeededAgent.Scenario("fine")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var scenarios = new ScenarioStore(database.Pool);
        var refund = (await scenarios.GetAsync(agent.Id, "refund", Cancellation))!;
        var chat = (await scenarios.GetAsync(agent.Id, "chat", Cancellation))!;
        var fine = (await scenarios.GetAsync(agent.Id, "fine", Cancellation))!;
        var first = await EnqueueAsync(store, agent);
        var second = await EnqueueAsync(store, agent);
        var outside = await EnqueueAsync(store, agent);
        async Task Add(Guid run, ScenarioRow scenario, int sample, string? error)
        {
            await store.AddSampleAsync(
                run,
                new NewSample(
                    scenario.Id,
                    scenario.Revision,
                    sample,
                    error is null ? SampleStatus.Done : SampleStatus.Failed,
                    error is null ? "[]" : null,
                    error,
                    null,
                    [],
                    []
                ),
                Cancellation
            );
        }
        await Add(first, refund, 1, "HTTP 500 first");
        await Add(first, refund, 2, null);
        await Add(first, chat, 1, "timeout");
        await Add(first, fine, 1, null);
        await Add(second, refund, 1, "HTTP 502 newest");
        await Add(second, refund, 2, "HTTP 503 newest too");
        await Add(second, chat, 1, null);
        await Add(second, fine, 1, null);
        await Add(outside, fine, 1, "not in the window");

        var errors = await store.ListAgentErrorsAsync([first, second], Cancellation);

        Assert.Equal(
            [("refund", 3, 4, second, "HTTP 502 newest"), ("chat", 1, 2, first, "timeout"), ("fine", 0, 2, null, null)],
            [.. errors.Select(row => (row.ScenarioKey, row.Errors, row.Samples, row.NewestErrorRunId, row.NewestError))]
        );
        Assert.All(errors, row => Assert.Equal(agent.VersionId, row.AgentVersionId));
        Assert.Empty(await store.ListAgentErrorsAsync([], Cancellation));
    }

    [Fact]
    public async Task A_sample_is_stored_once_per_scenario_and_number()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var id = await EnqueueAsync(store, agent);
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);
        var sample = new NewSample(scenario!.Id, scenario.Revision, 1, SampleStatus.Failed, null, "down", null, [], []);
        await store.AddSampleAsync(id, sample, Cancellation);

        await Assert.ThrowsAnyAsync<Exception>(() => store.AddSampleAsync(id, sample, Cancellation));
    }

    [Fact]
    public async Task Cancelling_a_queued_run_cancels_it_at_once()
    {
        var store = Store();
        var id = await EnqueueAsync(store, await NewAgentAsync());

        await store.RequestCancelAsync(id, Cancellation);

        var run = await store.GetAsync(id, Cancellation);
        Assert.Equal(RunStatus.Cancelled, run?.State);
        Assert.NotNull(run?.FinishedAt);
    }

    [Fact]
    public async Task Cancelling_a_running_run_only_marks_it_and_a_finished_run_stays()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var running = await EnqueueAsync(store, agent);
        await store.ClaimNextAsync(agent.Id, Cancellation);
        var finished = await EnqueueAsync(store, agent);
        await store.ClaimNextAsync(agent.Id, Cancellation);
        await store.FinishAsync(finished, RunStatus.Done, cancellationToken: Cancellation);

        await store.RequestCancelAsync(running, Cancellation);
        await store.RequestCancelAsync(finished, Cancellation);

        Assert.Equal(RunStatus.Running, (await store.GetAsync(running, Cancellation))?.State);
        Assert.True(await store.IsCancelRequestedAsync(running, Cancellation));
        Assert.Equal(RunStatus.Done, (await store.GetAsync(finished, Cancellation))?.State);
        Assert.False(await store.IsCancelRequestedAsync(finished, Cancellation));
    }

    [Fact]
    public async Task Finishing_records_the_status_the_error_and_the_time()
    {
        var store = Store();
        var id = await EnqueueAsync(store, await NewAgentAsync());

        await store.SetTotalAsync(id, 4, Cancellation);
        await store.FinishAsync(id, RunStatus.Failed, "boom", Cancellation);

        var run = await store.GetAsync(id, Cancellation);
        Assert.Equal((RunStatus.Failed, "boom", 4), (run?.State, run?.Error, run?.SamplesTotal));
        Assert.NotNull(run?.FinishedAt);
    }

    [Fact]
    public async Task List_gives_the_runs_of_an_agent_newest_first()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var first = await EnqueueAsync(store, agent);
        var second = await EnqueueAsync(store, agent);
        await EnqueueAsync(store, await NewAgentAsync());

        var runs = await store.ListAsync(agent.Id, Cancellation);

        Assert.Equal([second, first], runs.Select(run => run.Id));
    }

    private async Task SetCreatedAtAsync(Guid id, DateTime createdAt)
    {
        await using var connection = await database.Pool.OpenAsync(Cancellation);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE runs SET created_at = @createdAt WHERE id = @id",
                new { id, createdAt },
                cancellationToken: Cancellation
            )
        );
    }

    private async Task<IReadOnlyList<Guid>> PageAsync(SeededAgent agent, Guid? before, int limit) =>
        [.. (await Store().ListPageAsync(agent.Id, before, [], limit, Cancellation)).Select(run => run.Id)];

    [Fact]
    public async Task A_page_is_ordered_by_creation_time_descending_then_id_and_continues_after_before()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var moment = DateTime.UtcNow.AddDays(-1);
        var ids = new List<Guid>();
        for (var index = 0; index < 4; index++)
        {
            ids.Add(await EnqueueAsync(store, agent));
        }
        // Three runs created at the very same moment (ties go by id), one older.
        foreach (var id in ids.Take(3))
        {
            await SetCreatedAtAsync(id, moment);
        }
        await SetCreatedAtAsync(ids[3], moment.AddHours(-1));
        var expected = ids.Take(3).Order().Append(ids[3]).ToList();

        Assert.Equal(expected, await PageAsync(agent, null, 10));
        Assert.Equal(expected.Skip(1), await PageAsync(agent, expected[0], 10));
        Assert.Equal(expected.Skip(2).Take(1), await PageAsync(agent, expected[1], 1));
        Assert.Empty(await PageAsync(agent, expected[3], 10));
    }

    [Fact]
    public async Task A_page_is_limited_and_asking_for_one_more_shows_whether_there_is_more()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        for (var index = 0; index < 3; index++)
        {
            await EnqueueAsync(store, agent);
        }

        Assert.Equal(2, (await PageAsync(agent, null, 2)).Count);
        Assert.Equal(3, (await PageAsync(agent, null, 3)).Count);
        Assert.Equal(3, (await PageAsync(agent, null, 4)).Count);
        Assert.Empty(await PageAsync(await NewAgentAsync(), null, 5));
    }

    [Fact]
    public async Task A_page_can_be_narrowed_to_agent_versions()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var other = await NewAgentAsync();
        var first = await EnqueueAsync(store, agent);
        var second = await EnqueueAsync(store, other);

        var both = await store.ListPageAsync(null, null, [agent.VersionId, other.VersionId], 100, Cancellation);
        var onlyOther = await store.ListPageAsync(null, null, [other.VersionId], 100, Cancellation);

        Assert.Equal([second, first], both.Select(run => run.Id).Where(id => id == first || id == second));
        Assert.Equal([second], onlyOther.Select(run => run.Id));
    }

    private async Task<Guid> FinishedRunAsync(
        RunStore store,
        SeededAgent agent,
        bool[] samplePassed,
        RunStatus status = RunStatus.Done,
        Guid? recheckOf = null,
        bool[]? errored = null
    )
    {
        var id = await store.EnqueueAsync(agent.Id, agent.VersionId, null, agent.EndpointUrl, recheckOf, cancellationToken: Cancellation);
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);
        var check = Assert.Single(await new CheckStore(database.Pool).ListAsync(agent.Id, cancellationToken: Cancellation));
        for (var i = 0; i < samplePassed.Length; i++)
        {
            await store.AddSampleAsync(
                id,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    i + 1,
                    errored?[i] == true ? SampleStatus.Failed : SampleStatus.Done,
                    "[]",
                    errored?[i] == true ? "HTTP 500" : null,
                    1,
                    [],
                    [new NewCheckResult(check.Id, check.Revision, samplePassed[i], "[]", "m")]
                ),
                Cancellation
            );
        }
        await store.FinishAsync(id, status, cancellationToken: Cancellation);
        return id;
    }

    private static async Task<IReadOnlyList<RunCheckVerdictRow>> VerdictsAsync(RunStore store, SeededAgent agent, int maxRuns = 200) =>
        await store.ListRunCheckVerdictsAsync(EffectiveRuns.Pick(await store.ListAsync(agent.Id), maxRuns));

    [Fact]
    public async Task Run_check_verdicts_count_the_samples_of_finished_runs_of_the_agent_only()
    {
        var store = Store();
        var agent = await NewAgentAsync(threshold: 0.5);
        var other = await NewAgentAsync();
        var done = await FinishedRunAsync(store, agent, [true, false, true]);
        await FinishedRunAsync(store, agent, [true], RunStatus.Cancelled);
        await FinishedRunAsync(store, other, [true]);

        var row = Assert.Single(await VerdictsAsync(store, agent));

        Assert.Equal(
            (done, agent.VersionId, "refund", "refund-called", 1, 3, 2, 0.5),
            (
                row.RunId,
                row.AgentVersionId,
                row.ScenarioKey,
                row.CheckKey,
                row.CheckRevision,
                row.Samples,
                row.SamplesPassed,
                row.PassThreshold
            )
        );
    }

    [Fact]
    public async Task Run_check_verdicts_look_at_the_newest_runs_only()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await FinishedRunAsync(store, agent, [true]);
        var newest = await FinishedRunAsync(store, agent, [false]);

        var row = Assert.Single(await VerdictsAsync(store, agent, 1));

        Assert.Equal(newest, row.RunId);
    }

    [Fact]
    public async Task Run_check_verdicts_of_a_re_checked_run_are_the_re_checks_and_count_once()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var source = await FinishedRunAsync(store, agent, [true]);
        await FinishedRunAsync(store, agent, [false], recheckOf: source);

        var row = Assert.Single(await VerdictsAsync(store, agent));

        Assert.Equal((source, 1, 0), (row.RunId, row.Samples, row.SamplesPassed));
    }

    [Fact]
    public async Task A_check_fixed_and_re_checked_is_no_longer_unstable()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await FinishedRunAsync(store, agent, [true]);
        var failed = await FinishedRunAsync(store, agent, [false]);
        Assert.Single(UnstableChecks.Find(await VerdictsAsync(store, agent), [agent.VersionId]));

        await FinishedRunAsync(store, agent, [true], recheckOf: failed);

        Assert.Empty(UnstableChecks.Find(await VerdictsAsync(store, agent), [agent.VersionId]));
    }

    [Fact]
    public async Task Run_check_verdicts_leave_out_samples_where_the_agent_gave_no_answer()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        await FinishedRunAsync(store, agent, [true, false, false], errored: [false, true, true]);
        await FinishedRunAsync(store, agent, [false, false], errored: [true, true]);
        var failed = await FinishedRunAsync(store, agent, [false]);

        var rows = await VerdictsAsync(store, agent);

        Assert.Equal([(1, 1), (1, 0)], rows.OrderByDescending(row => row.SamplesPassed).Select(row => (row.Samples, row.SamplesPassed)));
        Assert.Equal(failed, Assert.Single(UnstableChecks.Find(rows, [agent.VersionId])).NewestFailedRunId);
    }
}
