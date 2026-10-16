using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class RunPlacementTests
{
    private static RunOrigin Origin(
        string? branch = "master",
        bool clean = true,
        bool pushed = true,
        string? defaultBranch = "master",
        string? target = null,
        string? compareWith = null
    ) =>
        new()
        {
            Branch = branch,
            Clean = clean,
            Pushed = pushed,
            DefaultBranch = defaultBranch,
            TargetBranch = target,
            CompareWith = compareWith,
        };

    private static RegressionSettings Branches(params string[] branches) => new() { Branches = branches };

    [Fact]
    public void A_run_without_git_is_no_reference_and_is_compared_with_every_run()
    {
        var placement = RunPlacement.Decide(null, Branches("master"));

        Assert.Equal((null, false, null), (placement.Origin, placement.Reference, placement.ComparedWith));
    }

    [Fact]
    public void A_clean_pushed_run_on_the_default_branch_is_a_reference_when_the_agent_names_no_branches()
    {
        var placement = RunPlacement.Decide(Origin(), null);

        Assert.Equal((true, "master"), (placement.Reference, placement.ComparedWith));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void A_dirty_or_unpushed_run_is_no_reference_but_is_still_compared_with_its_branch(bool clean, bool pushed)
    {
        var placement = RunPlacement.Decide(Origin(clean: clean, pushed: pushed), null);

        Assert.Equal((false, "master"), (placement.Reference, placement.ComparedWith));
    }

    [Fact]
    public void The_agents_branches_replace_the_default_branch_and_the_first_one_is_the_fallback()
    {
        var onRelease = RunPlacement.Decide(Origin("release", defaultBranch: "master"), Branches("main", "release"));
        var onFeature = RunPlacement.Decide(Origin("feature/x", defaultBranch: "main"), Branches("main", "release"));
        var onDefault = RunPlacement.Decide(Origin("master"), Branches("main", "release"));

        Assert.Equal((true, "release"), (onRelease.Reference, onRelease.ComparedWith));
        Assert.Equal((false, "main"), (onFeature.Reference, onFeature.ComparedWith));
        Assert.Equal((false, "main"), (onDefault.Reference, onDefault.ComparedWith));
    }

    [Fact]
    public void A_merge_request_is_compared_with_its_target_when_that_is_one_of_the_branches()
    {
        var targeted = RunPlacement.Decide(Origin("feature/x", target: "release"), Branches("main", "release"));
        var foreign = RunPlacement.Decide(Origin("feature/x", target: "other"), Branches("main", "release"));

        Assert.Equal("release", targeted.ComparedWith);
        Assert.Equal("main", foreign.ComparedWith);
    }

    [Fact]
    public void Compare_with_wins_over_everything_and_does_not_make_a_reference()
    {
        var placement = RunPlacement.Decide(Origin("feature/x", compareWith: "release", target: "main"), Branches("main", "release"));

        Assert.Equal((false, "release"), (placement.Reference, placement.ComparedWith));
    }

    [Fact]
    public void Without_a_branch_name_nor_a_default_branch_the_run_has_nothing_to_compare_with()
    {
        var placement = RunPlacement.Decide(Origin(null, defaultBranch: null), null);

        Assert.Equal((false, null), (placement.Reference, placement.ComparedWith));
    }
}
