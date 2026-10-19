namespace Suwageval.Contracts.Runs;

/// <summary>
/// Two to five runs of one agent side by side, everything a comparison shows: the first run is the baseline the others are compared with.
/// </summary>
public sealed record RunComparison
{
    /// <summary>In the order asked for.</summary>
    public required IReadOnlyList<ComparedRun> Runs { get; init; }

    /// <summary>The pass rate over every check first, then one row per tag, sorted by name.</summary>
    public required IReadOnlyList<ComparedTag> Tags { get; init; }

    /// <summary>The checks of all runs lined up by scenario key and check key, in the order they first appear (the baseline's first).</summary>
    public required IReadOnlyList<ComparedCheck> Checks { get; init; }
}
