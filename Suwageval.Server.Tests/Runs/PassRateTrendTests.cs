using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class PassRateTrendTests
{
    private static RunRow Run(RunStatus status, int minute, Guid? recheckOf = null) =>
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
            null,
            recheckOf
        );

    private static ScenarioRunResult Scenario(string key, params bool[] checks) => Scenario(key, [], checks);

    private static ScenarioRunResult Scenario(string key, int?[] durations, params bool[] checks) =>
        new()
        {
            Key = key,
            Revision = 1,
            Samples =
            [
                .. durations.Select(duration => new SampleResult
                {
                    Sample = 1,
                    Status = SampleStatus.Done,
                    DurationMs = duration,
                }),
            ],
            Checks =
            [
                .. checks.Select(
                    (passed, index) =>
                        new CheckRunResult
                        {
                            Key = $"check-{index}",
                            Revision = 1,
                            LatestRevision = 1,
                            JudgedByModel = false,
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
    public void A_check_counts_once_in_the_total_and_in_every_tag_of_its_scenario()
    {
        var rates = PassRateTrend.Rates(Results(Scenario("a", true, false), Scenario("b", true, true)), Tags);

        Assert.NotNull(rates);
        Assert.Equal(0.75, rates.Value.PassRate);
        Assert.Equal(
            [new TagPassRate("regression", 0.75, null, null), new TagPassRate("smoke", 0.5, null, null)],
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
        Assert.Equal([new TagPassRate("regression", 1.0, null, null)], rates.Value.Tags);
    }

    [Fact]
    public void A_run_without_checks_has_no_rates() => Assert.Null(PassRateTrend.Rates(Results(Scenario("a")), Tags));

    [Fact]
    public void Turn_time_is_the_median_and_p95_of_the_samples_that_made_a_call()
    {
        var rates = PassRateTrend.Rates(
            Results(Scenario("a", [1000, null, 3000], true), Scenario("b", [2000, 20_000], true), Scenario("gone", [500], true)),
            Tags
        );

        Assert.NotNull(rates);
        Assert.Equal((2000.0, 20_000.0), (rates.Value.MedianMs, rates.Value.P95Ms));
        var smoke = rates.Value.Tags.Single(tag => tag.Tag == "smoke");
        Assert.Equal((2000.0, 3000.0), (smoke.MedianMs, smoke.P95Ms));
        var regression = rates.Value.Tags.Single(tag => tag.Tag == "regression");
        Assert.Equal((2500.0, 20_000.0), (regression.MedianMs, regression.P95Ms));
    }

    [Fact]
    public void Turn_time_is_null_when_no_sample_made_a_call()
    {
        var rates = PassRateTrend.Rates(Results(Scenario("a", [null], true)), Tags);

        Assert.NotNull(rates);
        Assert.Null(rates.Value.MedianMs);
        Assert.Null(rates.Value.P95Ms);
        Assert.All(rates.Value.Tags, tag => Assert.Null(tag.MedianMs));
    }
}
