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
        Assert.All(picked, point => Assert.Equal(RunStatus.Done, point.Run.State));
        Assert.Equal(picked.Select(point => point.Run).OrderBy(run => run.CreatedAt), picked.Select(point => point.Run));
        Assert.Equal(newestFirst[2].Id, picked[^1].Run.Id);
        Assert.All(picked, point => Assert.Null(point.RecheckedBy));
    }

    [Fact]
    public void A_re_check_is_never_a_point_of_its_own()
    {
        var original = Run(RunStatus.Done, 1);

        var picked = PassRateTrend.Pick([Run(RunStatus.Done, 3, original.Id), original]);

        Assert.Equal(original.Id, Assert.Single(picked).Run.Id);
    }

    [Fact]
    public void A_point_shows_the_newest_finished_re_check_of_its_run()
    {
        var original = Run(RunStatus.Done, 1);
        var older = Run(RunStatus.Done, 2, original.Id);
        var newer = Run(RunStatus.Done, 3, original.Id);
        var running = Run(RunStatus.Running, 4, original.Id);
        var other = Run(RunStatus.Done, 0);

        var picked = PassRateTrend.Pick([running, newer, older, original, other]);

        Assert.Equal([other.Id, original.Id], picked.Select(point => point.Run.Id));
        Assert.Equal([null, newer.Id], picked.Select(point => point.RecheckedBy?.Id));
    }

    [Fact]
    public void A_re_check_still_running_changes_nothing()
    {
        var original = Run(RunStatus.Done, 1);

        var picked = PassRateTrend.Pick([Run(RunStatus.Running, 2, original.Id), Run(RunStatus.Queued, 3, original.Id), original]);

        Assert.Null(Assert.Single(picked).RecheckedBy);
    }

    [Fact]
    public void The_cap_counts_original_runs_only()
    {
        var originals = Enumerable.Range(0, 60).Select(minute => Run(RunStatus.Done, minute)).ToList();
        var rechecks = originals.Select(run => Run(RunStatus.Done, 100, run.Id));
        var newestFirst = rechecks.Concat(Enumerable.Reverse(originals)).ToList();

        var picked = PassRateTrend.Pick(newestFirst);

        Assert.Equal(PassRateTrend.MaxRuns, picked.Count);
        Assert.All(picked, point => Assert.NotNull(point.RecheckedBy));
    }

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

    [Fact]
    public void Median_takes_the_middle_value_or_the_mean_of_the_two_middle_ones()
    {
        Assert.Equal(3.0, PassRateTrend.Median([5, 1, 3]));
        Assert.Equal(2.5, PassRateTrend.Median([4, 1, 2, 3]));
        Assert.Null(PassRateTrend.Median([]));
    }

    [Fact]
    public void P95_is_the_value_at_rank_ceiling_of_ninety_five_percent()
    {
        Assert.Equal(19.0, PassRateTrend.P95([.. Enumerable.Range(1, 20)]));
        Assert.Equal(4.0, PassRateTrend.P95([4, 1, 2, 3]));
        Assert.Equal(7.0, PassRateTrend.P95([7]));
        Assert.Null(PassRateTrend.P95([]));
    }
}
