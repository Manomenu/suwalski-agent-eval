namespace Suwageval.Contracts.Runs;

/// <summary>
/// Runs of one agent side by side (typically one per model or configuration, each its own agent version): a column per run, the pass
/// rate per tag and a row per scenario. When a baseline was asked for, also how the other runs stand against it.
/// </summary>
public sealed record RunComparison
{
    public required string Agent { get; init; }

    /// <summary>The group the runs were picked by; none when they were named one by one.</summary>
    public string? Group { get; init; }

    /// <summary>The columns: in the order asked for, or by creation time for a group.</summary>
    public required IReadOnlyList<VariantRun> Runs { get; init; }

    /// <summary>The pass rate over every check first, then one row per tag; the rates carry no changes (those are in <see cref="Baseline"/>).</summary>
    public required IReadOnlyList<ComparedTag> Tags { get; init; }

    /// <summary>Every scenario any run ran, sorted by key.</summary>
    public required IReadOnlyList<VariantScenario> Scenarios { get; init; }

    /// <summary>The runs against the baseline; absent when no baseline was asked for.</summary>
    public BaselineComparison? Baseline { get; init; }
}
