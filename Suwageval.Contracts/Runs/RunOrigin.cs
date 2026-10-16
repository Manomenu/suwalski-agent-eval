namespace Suwageval.Contracts.Runs;

/// <summary>
/// Where the agent's repository stood when a run was started: the facts the CLI reads from git (or from the CI's environment). The
/// server decides from them whether the run is a reference run and which branch's history it is compared with.
/// </summary>
public sealed record RunOrigin
{
    /// <summary>The checked-out branch; none on a detached HEAD the CI did not name.</summary>
    public string? Branch { get; init; }

    public string? Commit { get; init; }

    /// <summary>True when the tree has no uncommitted changes.</summary>
    public required bool Clean { get; init; }

    /// <summary>True when the commit is on the remote branch, not only in a local one.</summary>
    public required bool Pushed { get; init; }

    /// <summary>The repository's default branch (<c>origin/HEAD</c>, or the CI's), used when the agent names no branches.</summary>
    public string? DefaultBranch { get; init; }

    /// <summary>The branch a pull or merge request targets, when the run comes from one in CI.</summary>
    public string? TargetBranch { get; init; }

    /// <summary>The branch to compare with, given by hand (<c>--ref</c>); it wins over everything the run would pick itself.</summary>
    public string? CompareWith { get; init; }
}
