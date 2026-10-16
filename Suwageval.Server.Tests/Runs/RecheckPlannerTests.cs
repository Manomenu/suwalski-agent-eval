using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class RecheckPlannerTests
{
    private static RecheckPlan Plan(RunStatus status, params ScenarioResultRow[] samples) =>
        RecheckPlanner.Of(status, samples, samples, [], [], [], SeededAgent.ExampleVersion);

    private static ScenarioResultRow Sample(string key) => new(Guid.NewGuid(), key, 1, 1, "Done", "[]", null, "[]", 10, Guid.NewGuid());

    [Theory]
    [InlineData(RunStatus.Queued)]
    [InlineData(RunStatus.Running)]
    [InlineData(RunStatus.Failed)]
    [InlineData(RunStatus.Cancelled)]
    public void Only_a_done_run_can_be_re_checked(RunStatus status)
    {
        var plan = Plan(status, Sample("refund"));

        Assert.False(plan.Possible);
        Assert.Equal(RecheckBlocker.NotFinished, plan.Blocker);
        Assert.Contains(status.ToString(), plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void When_no_scenario_of_the_run_is_live_any_more_every_one_is_skipped_and_nothing_is_possible()
    {
        var plan = Plan(RunStatus.Done, Sample("b"), Sample("a"));

        Assert.False(plan.Possible);
        Assert.Equal(RecheckBlocker.AllSkipped, plan.Blocker);
        Assert.Equal(["a", "b"], plan.SkippedScenarios.Select(skipped => skipped.Key));
        Assert.Empty(plan.ChangedChecks);
    }
}
