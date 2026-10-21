namespace Suwageval.Contracts.Runs;

/// <summary>
/// Runs of one agent side by side as variants (typically one per model or configuration, each its own agent version): a column per run,
/// the pass rate per tag and a row per scenario.
/// </summary>
public sealed record RunVariants
{
    public required string Agent { get; init; }

    /// <summary>The group the runs were picked by; none when they were named one by one.</summary>
    public string? Group { get; init; }

    /// <summary>The columns: in the order asked for, or by creation time for a group.</summary>
    public required IReadOnlyList<VariantRun> Runs { get; init; }

    /// <summary>The pass rate over every check first, then one row per tag, as in a comparison (the changes against the first run are not meant here).</summary>
    public required IReadOnlyList<ComparedTag> Tags { get; init; }

    /// <summary>Every scenario any run ran, sorted by key.</summary>
    public required IReadOnlyList<VariantScenario> Scenarios { get; init; }
}
