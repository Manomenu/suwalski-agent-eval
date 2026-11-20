namespace Suwageval.Contracts.Runs;

public sealed record VariantRun
{
    public required RunSummary Run { get; init; }

    public string? VersionLabel { get; init; }

    public required IReadOnlyList<string> ModelNames { get; init; }

    public string? JudgeModel { get; init; }

    public double? MedianMs { get; init; }

    public double? P95Ms { get; init; }
}
