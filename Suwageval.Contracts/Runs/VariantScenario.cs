namespace Suwageval.Contracts.Runs;

/// <summary>A row of the compare view: one scenario and what each run made of it.</summary>
public sealed record VariantScenario
{
    public required string Key { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>One per run, in the order of <see cref="RunComparison.Runs"/>; null for a run that did not run the scenario.</summary>
    public required IReadOnlyList<VariantCell?> Cells { get; init; }
}
