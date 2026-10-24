using Suwageval.Contracts.Runs;
using Suwageval.Server.Evaluation.Runs.Trend;

namespace Suwageval.Server.Tests.Evaluation.Runs.Trend;

public sealed class PassRateTrendTests
{
    /// <summary>A scenario's contribution: one verdict per given flag and one duration per given value (null: no call, so none).</summary>
    private sealed record Scenario(string Key, int?[] Durations, bool[] Checks);

    private static Scenario Of(string key, params bool[] checks) => new(key, [], checks);

    private static Scenario Of(string key, int?[] durations, params bool[] checks) => new(key, durations, checks);

    private static (double PassRate, double? MedianMs, double? P95Ms, IReadOnlyList<TagPassRate> Tags)? Rates(
        params Scenario[] scenarios
    ) => RatesOfCompleteTags(new HashSet<string> { "smoke", "regression" }, scenarios);

    private static (double PassRate, double? MedianMs, double? P95Ms, IReadOnlyList<TagPassRate> Tags)? RatesOfCompleteTags(
        IReadOnlySet<string> completeTags,
        params Scenario[] scenarios
    ) =>
        PassRateTrend.Rates(
            [.. scenarios.SelectMany(scenario => scenario.Checks.Select(passed => (scenario.Key, passed)))],
            [.. scenarios.SelectMany(scenario => scenario.Durations.OfType<int>().Select(duration => (scenario.Key, duration)))],
            Tags,
            completeTags
        );

    private static readonly Dictionary<string, IReadOnlyList<string>> Tags = new()
    {
        ["a"] = ["smoke", "regression"],
        ["b"] = ["regression"],
    };

    [Fact]
    public void A_check_counts_once_in_the_total_and_in_every_tag_of_its_scenario()
    {
        var rates = Rates(Of("a", true, false), Of("b", true, true));

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
        var rates = Rates(Of("gone", false), Of("b", true));

        Assert.NotNull(rates);
        Assert.Equal(0.5, rates.Value.PassRate);
        Assert.Equal([new TagPassRate("regression", 1.0, null, null)], rates.Value.Tags);
    }

    [Fact]
    public void A_tag_the_run_did_not_cover_whole_has_no_rate_but_its_checks_count_in_the_total()
    {
        var rates = RatesOfCompleteTags(new HashSet<string> { "regression" }, Of("a", true, false), Of("b", true, true));

        Assert.NotNull(rates);
        Assert.Equal(0.75, rates.Value.PassRate);
        Assert.Equal(["regression"], rates.Value.Tags.Select(tag => tag.Tag));
    }

    [Fact]
    public void A_run_without_checks_has_no_rates() => Assert.Null(Rates(Of("a")));

    [Fact]
    public void Turn_time_is_the_median_and_p95_of_the_samples_that_made_a_call()
    {
        var rates = Rates(Of("a", [1000, null, 3000], true), Of("b", [2000, 20_000], true), Of("gone", [500], true));

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
        var rates = Rates(Of("a", [null], true));

        Assert.NotNull(rates);
        Assert.Null(rates.Value.MedianMs);
        Assert.Null(rates.Value.P95Ms);
        Assert.All(rates.Value.Tags, tag => Assert.Null(tag.MedianMs));
    }
}
