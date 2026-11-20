namespace Suwageval.Contracts.Runs;

public sealed record VariantCell
{
    public required int ChecksPassed { get; init; }

    public required int ChecksTotal { get; init; }

    public required IReadOnlyList<string> FailedChecks { get; init; }

    public required IReadOnlyList<VariantCheck> Checks { get; init; }

    public required int Samples { get; init; }

    public required int SamplesFailed { get; init; }

    public required IReadOnlyList<VariantNoAnswer> NoAnswers { get; init; }
}
