using System.Text.Json;
using Dapper;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.Tests.Evaluation.Runs;

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

    private static Task<Guid> EnqueueAsync(RunStore store, SeededAgent agent, params string[] tags) =>
        store.EnqueueAsync(agent.Id, agent.VersionId, tags, agent.EndpointUrl, cancellationToken: Cancellation);

    [Fact]
    public async Task An_enqueued_run_is_queued_with_no_results()
    {
        var store = Store();
        var agent = await NewAgentAsync();

        var id = await EnqueueAsync(store, agent, "regression");

        var run = await store.GetAsync(id, Cancellation);
        Assert.NotNull(run);
        Assert.Equal((agent.Name, agent.VersionId, "http://agent.test"), (run.AgentName, run.AgentVersionId, run.EndpointUrl));
        Assert.Equal(["regression"], run.Tags);
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
    public async Task The_check_counts_are_checks_that_reached_their_threshold_not_samples()
    {
        var store = Store();
        var agent = await NewAgentAsync(threshold: 0.5);
        var id = await EnqueueAsync(store, agent);
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);
        var check = Assert.Single(await new CheckStore(database.Pool).ListAsync(agent.Id, cancellationToken: Cancellation));

        foreach (var (sample, passed) in new[] { (1, true), (2, false), (3, true), (4, false) })
        {
            await store.AddSampleAsync(
                id,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    sample,
                    SampleStatus.Done,
                    "[]",
                    null,
                    10,
                    [],
                    [new NewCheckResult(check.Id, check.Revision, passed, "[]", "m")]
                ),
                Cancellation
            );
        }

        var run = await store.GetAsync(id, Cancellation);
        Assert.Equal((4, 1, 0), (run?.SamplesDone, run?.ChecksPassed, run?.ChecksFailed));
    }

    [Fact]
    public async Task A_sample_without_an_agent_answer_is_counted_as_failed()
    {
        var store = Store();
        var agent = await NewAgentAsync(threshold: 0.5);
        var id = await EnqueueAsync(store, agent);
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);

        foreach (var (sample, status) in new[] { (1, SampleStatus.Done), (2, SampleStatus.Failed) })
        {
            await store.AddSampleAsync(
                id,
                new NewSample(scenario!.Id, scenario.Revision, sample, status, "[]", null, 10, [], []),
                Cancellation
            );
        }

        var run = await store.GetAsync(id, Cancellation);
        Assert.Equal((2, 1), (run?.SamplesDone, run?.SamplesFailed));
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
    public async Task Interrupting_marks_only_the_running_runs_with_the_reason_and_the_time()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var running = await EnqueueAsync(store, agent);
        await store.ClaimNextAsync(agent.Id, Cancellation);
        var queued = await EnqueueAsync(store, agent);
        var done = await EnqueueAsync(store, agent);
        await store.FinishAsync(done, RunStatus.Done, cancellationToken: Cancellation);

        var marked = await store.InterruptRunningAsync(agent.Id, Cancellation);

        Assert.Equal(1, marked);
        var interrupted = await store.GetAsync(running, Cancellation);
        Assert.Equal((RunStatus.Interrupted, RunStore.InterruptedError), (interrupted?.State, interrupted?.Error));
        Assert.NotNull(interrupted?.FinishedAt);
        Assert.Equal(RunStatus.Queued, (await store.GetAsync(queued, Cancellation))?.State);
        var untouched = await store.GetAsync(done, Cancellation);
        Assert.Equal((RunStatus.Done, null), (untouched?.State, untouched?.Error));
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

    private async Task<IReadOnlyList<Guid>> PageAsync(SeededAgent agent, int offset, int limit) =>
        [.. (await Store().ListPageAsync(agent.Id, [], offset, limit, cancellationToken: Cancellation)).Select(run => run.Id)];

    [Fact]
    public async Task A_page_is_ordered_by_creation_time_descending_then_id_and_continues_after_the_offset()
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

        Assert.Equal(expected, await PageAsync(agent, 0, 10));
        Assert.Equal(expected.Skip(1), await PageAsync(agent, 1, 10));
        Assert.Equal(expected.Skip(2).Take(1), await PageAsync(agent, 2, 1));
        Assert.Empty(await PageAsync(agent, 4, 10));
    }

    [Fact]
    public async Task A_page_is_limited_and_the_count_covers_every_page()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        for (var index = 0; index < 3; index++)
        {
            await EnqueueAsync(store, agent);
        }

        Assert.Equal(2, (await PageAsync(agent, 0, 2)).Count);
        Assert.Single(await PageAsync(agent, 2, 2));
        Assert.Equal(3, await store.CountAsync(agent.Id, [], cancellationToken: Cancellation));
        Assert.Empty(await PageAsync(await NewAgentAsync(), 0, 5));
    }

    [Fact]
    public async Task A_page_can_be_narrowed_to_agent_versions()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var other = await NewAgentAsync();
        var first = await EnqueueAsync(store, agent);
        var second = await EnqueueAsync(store, other);

        var both = await store.ListPageAsync(null, [agent.VersionId, other.VersionId], 0, 100, cancellationToken: Cancellation);
        var onlyOther = await store.ListPageAsync(null, [other.VersionId], 0, 100, cancellationToken: Cancellation);

        Assert.Equal([second, first], both.Select(run => run.Id).Where(id => id == first || id == second));
        Assert.Equal([second], onlyOther.Select(run => run.Id));
    }

    [Fact]
    public async Task A_page_can_be_narrowed_to_tags_a_run_ran_and_the_count_follows()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var smoke = await EnqueueAsync(store, agent, "smoke");
        var both = await EnqueueAsync(store, agent, "billing", "smoke");
        var billing = await EnqueueAsync(store, agent, "billing");
        var everything = await EnqueueAsync(store, agent);

        async Task<IReadOnlyList<Guid>> ForAsync(params string[] tags)
        {
            return [.. (await store.ListPageAsync(agent.Id, [], 0, 10, tags: tags, cancellationToken: Cancellation)).Select(run => run.Id)];
        }

        Assert.Equal([everything, billing, both, smoke], await ForAsync());
        Assert.Equal([everything, both, smoke], await ForAsync("smoke"));
        Assert.Equal([everything, billing, both, smoke], await ForAsync("smoke", "billing"));
        Assert.Equal([everything], await ForAsync("nothing"));
        Assert.Equal(3, await store.CountAsync(agent.Id, [], tags: ["smoke"], cancellationToken: Cancellation));
        Assert.Equal(1, await store.CountAsync(agent.Id, [], tags: ["nothing"], cancellationToken: Cancellation));
        Assert.Equal(
            [both],
            (await store.ListPageAsync(agent.Id, [], 1, 1, tags: ["smoke"], cancellationToken: Cancellation)).Select(run => run.Id)
        );
    }
}
