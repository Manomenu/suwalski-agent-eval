using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class EffectiveRunsTests
{
    private static RunRow Run(RunStatus status, int minute, Guid? recheckOf = null, string? runGroup = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "agent",
            Guid.NewGuid(),
            "[]",
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
            recheckOf,
            RunGroup: runGroup
        );

    [Fact]
    public void Only_finished_runs_count_the_newest_fifty_oldest_first()
    {
        var newestFirst = Enumerable
            .Range(0, 60)
            .Select(minute => Run(RunStatus.Done, 59 - minute))
            .Prepend(Run(RunStatus.Cancelled, 100))
            .Prepend(Run(RunStatus.Failed, 101))
            .ToList();

        var picked = EffectiveRuns.Pick(newestFirst, PassRateTrend.MaxRuns);

        Assert.Equal(PassRateTrend.MaxRuns, picked.Count);
        Assert.All(picked, point => Assert.Equal(RunStatus.Done, point.Run.State));
        Assert.Equal(picked.Select(point => point.Run).OrderBy(run => run.CreatedAt), picked.Select(point => point.Run));
        Assert.Equal(newestFirst[2].Id, picked[^1].Run.Id);
        Assert.All(picked, point => Assert.Null(point.RecheckedBy));
    }

    [Fact]
    public void A_run_started_in_a_group_is_an_experiment_and_never_a_point()
    {
        var plain = Run(RunStatus.Done, 1);

        var picked = EffectiveRuns.Pick([Run(RunStatus.Done, 2, runGroup: "sweep-1"), plain], PassRateTrend.MaxRuns);

        Assert.Equal(plain.Id, Assert.Single(picked).Run.Id);
    }

    [Fact]
    public void A_re_check_is_never_a_point_of_its_own()
    {
        var original = Run(RunStatus.Done, 1);

        var picked = EffectiveRuns.Pick([Run(RunStatus.Done, 3, original.Id), original], PassRateTrend.MaxRuns);

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

        var picked = EffectiveRuns.Pick([running, newer, older, original, other], PassRateTrend.MaxRuns);

        Assert.Equal([other.Id, original.Id], picked.Select(point => point.Run.Id));
        Assert.Equal([null, newer.Id], picked.Select(point => point.RecheckedBy?.Id));
    }

    [Fact]
    public void A_re_check_still_running_changes_nothing()
    {
        var original = Run(RunStatus.Done, 1);

        var picked = EffectiveRuns.Pick(
            [Run(RunStatus.Running, 2, original.Id), Run(RunStatus.Queued, 3, original.Id), original],
            PassRateTrend.MaxRuns
        );

        Assert.Null(Assert.Single(picked).RecheckedBy);
    }

    [Fact]
    public void The_cap_counts_original_runs_only()
    {
        var originals = Enumerable.Range(0, 60).Select(minute => Run(RunStatus.Done, minute)).ToList();
        var rechecks = originals.Select(run => Run(RunStatus.Done, 100, run.Id));
        var newestFirst = rechecks.Concat(Enumerable.Reverse(originals)).ToList();

        var picked = EffectiveRuns.Pick(newestFirst, PassRateTrend.MaxRuns);

        Assert.Equal(PassRateTrend.MaxRuns, picked.Count);
        Assert.All(picked, point => Assert.NotNull(point.RecheckedBy));
    }
}
