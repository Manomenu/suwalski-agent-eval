namespace Suwageval.Contracts.Runs;

public sealed record VariantCheck
{
    public required string Key { get; init; }

    public required bool Passed { get; init; }

    public required double PassRate { get; init; }

    public required int Samples { get; init; }

    public required int SamplesPassed { get; init; }

    public required bool JudgedByModel { get; init; }

    public required IReadOnlyList<VariantFailure> Failures { get; init; }
}
