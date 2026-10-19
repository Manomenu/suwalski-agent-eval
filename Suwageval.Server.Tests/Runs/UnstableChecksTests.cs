using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class UnstableChecksTests
{
    private static readonly Guid Version = Guid.NewGuid();

    private static RunCheckVerdictRow Row(
        bool passed,
        string check = "c",
        int minute = 0,
        Guid? version = null,
        int revision = 1,
        string scenario = "s",
        Guid? run = null
    ) =>
        new(
            run ?? Guid.NewGuid(),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(minute),
            version ?? Version,
            scenario,
            check,
            revision,
            Samples: 1,
            SamplesPassed: passed ? 1 : 0,
            PassThreshold: 1.0
        );

    private static IReadOnlyList<Contracts.Runs.UnstableCheck> Find(IEnumerable<RunCheckVerdictRow> rows, params Guid[] newestFirst) =>
        UnstableChecks.Find(rows, newestFirst.Length == 0 ? [Version] : newestFirst);

    [Fact]
    public void A_check_that_passed_in_some_runs_and_failed_in_others_is_unstable()
    {
        var failed = Guid.NewGuid();

        var unstable = Assert.Single(Find([Row(true, minute: 0), Row(false, minute: 1), Row(false, minute: 2, run: failed)]));

        Assert.Equal(
            ("s", "c", 1, Version, 3, 1, failed),
            (
                unstable.ScenarioKey,
                unstable.CheckKey,
                unstable.CheckRevision,
                unstable.AgentVersionId,
                unstable.Runs,
                unstable.PassedRuns,
                unstable.NewestFailedRunId
            )
        );
    }

    [Fact]
    public void Stable_checks_and_a_single_run_are_not_reported() =>
        Assert.Empty(Find([Row(true), Row(true), Row(false, "fails"), Row(false, "fails"), Row(false, "once")]));

    [Fact]
    public void Versions_and_revisions_are_separate_checks()
    {
        var other = Guid.NewGuid();

        Assert.Empty(Find([Row(true), Row(false, version: other), Row(true, revision: 2), Row(false, revision: 3)]));
    }

    [Fact]
    public void A_run_counts_by_the_threshold_over_its_samples()
    {
        var reached = Row(true) with { Samples = 5, SamplesPassed = 4, PassThreshold = 0.8 };
        var missed = Row(true) with { Samples = 5, SamplesPassed = 3, PassThreshold = 0.8 };

        var unstable = Assert.Single(Find([reached, missed]));

        Assert.Equal((2, 1), (unstable.Runs, unstable.PassedRuns));
    }

    [Fact]
    public void The_share_of_failed_runs_decides_the_order_then_the_keys()
    {
        var found = Find([
            Row(true, "b"),
            Row(false, "b"),
            Row(true, "a"),
            Row(false, "a"),
            Row(true, "rare"),
            Row(true, "rare"),
            Row(false, "rare"),
            Row(false, "often"),
            Row(false, "often"),
            Row(true, "often"),
        ]);

        Assert.Equal(["often", "a", "b", "rare"], found.Select(check => check.CheckKey));
    }

    private static readonly Guid Newer = Guid.NewGuid();

    [Fact]
    public void A_newer_version_that_ran_the_check_twice_without_the_problem_closes_it()
    {
        var rows = new[] { Row(true), Row(false), Row(true, version: Newer), Row(true, version: Newer) };

        Assert.Empty(Find(rows, Newer, Version));
    }

    [Fact]
    public void A_newer_version_that_ran_the_check_once_closes_nothing_yet()
    {
        var rows = new[] { Row(true), Row(false), Row(true, version: Newer) };

        Assert.Equal(Version, Assert.Single(Find(rows, Newer, Version)).AgentVersionId);
    }

    [Fact]
    public void The_newest_version_decides_even_when_it_is_the_unstable_one()
    {
        var rows = new[] { Row(true), Row(true), Row(true, version: Newer), Row(false, version: Newer) };

        Assert.Equal(Newer, Assert.Single(Find(rows, Newer, Version)).AgentVersionId);
    }

    [Fact]
    public void A_version_that_is_not_considered_closes_nothing()
    {
        var rows = new[] { Row(true), Row(false), Row(true, version: Newer), Row(true, version: Newer) };

        Assert.Equal(Version, Assert.Single(Find(rows, Version)).AgentVersionId);
    }

    [Fact]
    public void A_newer_revision_run_twice_closes_the_older_revisions_but_one_run_does_not()
    {
        var revisionOne = new[] { Row(true), Row(false) };

        Assert.Empty(Find([.. revisionOne, Row(true, revision: 2), Row(true, revision: 2)]));
        Assert.Empty(
            Find([.. revisionOne, Row(true, revision: 2, version: Newer), Row(true, revision: 2, version: Newer)], Newer, Version)
        );
        Assert.Single(Find([.. revisionOne, Row(true, revision: 2)]));
    }
}
