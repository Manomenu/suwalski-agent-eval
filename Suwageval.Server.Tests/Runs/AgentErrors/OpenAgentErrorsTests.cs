using Suwageval.Server.Runs.AgentErrors;

namespace Suwageval.Server.Tests.Runs.AgentErrors;

public sealed class OpenAgentErrorsTests
{
    private static readonly Guid Older = Guid.NewGuid();
    private static readonly Guid Newer = Guid.NewGuid();

    private static AgentErrorRow Row(Guid version, int errors, string scenario = "s") =>
        new(version, scenario, 2, errors, errors > 0 ? Guid.NewGuid() : null, errors > 0 ? "boom" : null);

    [Fact]
    public void Errors_of_the_newest_version_that_ran_the_scenario_are_open()
    {
        var open = Assert.Single(OpenAgentErrors.Find([Row(Older, 0), Row(Newer, 1)], [Newer, Older]));

        Assert.Equal(Newer, open.AgentVersionId);
    }

    [Fact]
    public void A_newer_version_without_errors_closes_the_older_ones() =>
        Assert.Empty(OpenAgentErrors.Find([Row(Older, 2), Row(Newer, 0)], [Newer, Older]));

    [Fact]
    public void A_newer_version_that_did_not_run_the_scenario_closes_nothing() =>
        Assert.Single(OpenAgentErrors.Find([Row(Older, 1), Row(Newer, 0, "other")], [Newer, Older]));

    [Fact]
    public void A_version_that_is_not_considered_closes_nothing_and_shows_nothing() =>
        Assert.Equal(
            Older,
            Assert.Single(OpenAgentErrors.Find([Row(Older, 1), Row(Newer, 0), Row(Newer, 3, "x")], [Older])).AgentVersionId
        );

    [Fact]
    public void The_order_of_the_rows_is_kept()
    {
        var found = OpenAgentErrors.Find([Row(Newer, 1, "b"), Row(Newer, 1, "a")], [Newer]);

        Assert.Equal(["b", "a"], found.Select(row => row.ScenarioKey));
    }
}
