namespace Suwageval.Contracts.Runs;

public sealed record VariantScenario
{
    public required string Key { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<VariantCell?> Cells { get; init; }
}
