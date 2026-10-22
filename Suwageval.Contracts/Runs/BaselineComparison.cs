namespace Suwageval.Contracts.Runs;

/// <summary>What a comparison adds when one of its runs is chosen as the baseline: how the others stand against it.</summary>
public sealed record BaselineComparison
{
    /// <summary>The baseline run: one of the compared runs.</summary>
    public required Guid RunId { get; init; }

    /// <summary>One per compared run, in the order of the columns; the baseline's own entry has no changes.</summary>
    public required IReadOnlyList<ComparedRun> Runs { get; init; }

    /// <summary>The pass rate over every check first, then one row per tag, sorted by name; every rate carries its change against the baseline.</summary>
    public required IReadOnlyList<ComparedTag> Tags { get; init; }

    /// <summary>The checks of all runs lined up by scenario key and check key, in the order they first appear (the baseline's first).</summary>
    public required IReadOnlyList<ComparedCheck> Checks { get; init; }
}
