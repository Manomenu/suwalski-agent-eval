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
        store.EnqueueAsync(agent.Id, agent.VersionId, tag, agent.EndpointUrl, Cancellation);

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
    }

    [Fact]
    public async Task A_sample_is_stored_once_per_scenario_and_number()
    {
        var store = Store();
        var agent = await NewAgentAsync();
        var id = await EnqueueAsync(store, agent);
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);
        var sample = new NewSample(scenario!.Id, scenario.Revision, 1, SampleStatus.Failed, null, "down", [], []);
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
}
