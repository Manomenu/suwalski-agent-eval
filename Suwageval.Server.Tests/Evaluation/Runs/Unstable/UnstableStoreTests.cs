using Suwageval.Contracts.Runs;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Evaluation.Runs.Unstable;
using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.Tests.Evaluation.Runs.Unstable;

public sealed class UnstableStoreTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private UnstableStore Unstable() => new(database.Pool);

    private RunStore Store() => new(database.Pool);

    private Task<SeededAgent> NewAgentAsync(double threshold = 1.0) =>
        SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund", threshold)]
        );

    private async Task<Guid> FinishedRunAsync(
        RunStore store,
        SeededAgent agent,
        bool[] samplePassed,
        RunStatus status = RunStatus.Done,
        Guid? recheckOf = null,
        bool[]? errored = null
    )
    {
        var id = await store.EnqueueAsync(agent.Id, agent.VersionId, [], agent.EndpointUrl, recheckOf, cancellationToken: Cancellation);
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

    private async Task<IReadOnlyList<RunCheckVerdictRow>> VerdictsAsync(RunStore store, SeededAgent agent, int maxRuns = 200) =>
        await Unstable().ListRunCheckVerdictsAsync(EffectiveRuns.Pick(await store.ListAsync(agent.Id), maxRuns));

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
