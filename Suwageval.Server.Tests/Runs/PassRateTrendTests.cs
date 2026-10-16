using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class PassRateTrendTests
{
    private static RunRow Run(RunStatus status, int minute) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "agent",
            Guid.NewGuid(),
            null,
            "http://agent.test",
            status.ToString(),
            null,
            0,
            0,
            0,
            0,
            false,
            false,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(minute),
            null,
            null
        );

    private static ScenarioRunResult Scenario(string key, params bool[] checks) =>
        new()
        {
            Key = key,
            Revision = 1,
            Samples = [],
            Checks =
            [
                .. checks.Select(
                    (passed, index) =>
                        new CheckRunResult
                        {
                            Key = $"check-{index}",
                            Revision = 1,
                            LatestRevision = 1,
                            Definition = SeededAgent.RefundAfterLookup($"check-{index}", key),
                            Passed = passed,
                            PassRate = passed ? 1 : 0,
                            PassThreshold = 1,
                            Samples = [],
                        }
                ),
            ],
        };

    private static RunResults Results(params ScenarioRunResult[] scenarios) =>
        new() { Run = RunReport.Summary(Run(RunStatus.Done, 0)), Scenarios = scenarios };

    private static readonly Dictionary<string, IReadOnlyList<string>> Tags = new()
    {
        ["a"] = ["smoke", "regression"],
        ["b"] = ["regression"],
    };

    [Fact]
    public void Only_finished_runs_count_the_newest_fifty_oldest_first()
    {
        var newestFirst = Enumerable
            .Range(0, 60)
            .Select(minute => Run(RunStatus.Done, 59 - minute))
            .Prepend(Run(RunStatus.Cancelled, 100))
            .Prepend(Run(RunStatus.Failed, 101))
            .ToList();

        var picked = PassRateTrend.Pick(newestFirst);

        Assert.Equal(PassRateTrend.MaxRuns, picked.Count);
        Assert.All(picked, run => Assert.Equal(RunStatus.Done, run.State));
        Assert.Equal(picked.OrderBy(run => run.CreatedAt), picked);
        Assert.Equal(newestFirst[2].Id, picked[^1].Id);
    }

    [Fact]
    public void A_check_counts_once_in_the_total_and_in_every_tag_of_its_scenario()
    {
        var rates = PassRateTrend.Rates(Results(Scenario("a", true, false), Scenario("b", true, true)), Tags);

        Assert.NotNull(rates);
        Assert.Equal(0.75, rates.Value.PassRate);
        Assert.Equal(
            [new TagPassRate("regression", 0.75), new TagPassRate("smoke", 0.5)],
            rates.Value.Tags.OrderBy(tag => tag.Tag).ToList()
        );
        Assert.Equal(["regression", "smoke"], rates.Value.Tags.Select(tag => tag.Tag));
    }

    [Fact]
    public void A_scenario_that_is_gone_counts_in_the_total_only()
    {
        var rates = PassRateTrend.Rates(Results(Scenario("gone", false), Scenario("b", true)), Tags);

        Assert.NotNull(rates);
        Assert.Equal(0.5, rates.Value.PassRate);
        Assert.Equal([new TagPassRate("regression", 1.0)], rates.Value.Tags);
    }

    [Fact]
    public void A_run_without_checks_has_no_rates() => Assert.Null(PassRateTrend.Rates(Results(Scenario("a")), Tags));
}
