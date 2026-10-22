namespace Suwageval.Contracts.Runs;

/// <summary>A column of the compare view: the run, the variant it is of and its turn times.</summary>
public sealed record VariantRun
{
    public required RunSummary Run { get; init; }

    /// <summary>The label the run's version carries: the name of the variant.</summary>
    public string? VersionLabel { get; init; }

    /// <summary>The models the run's version uses.</summary>
    public required IReadOnlyList<string> ModelNames { get; init; }

    /// <summary>The median turn time over the run's samples that made a call, in milliseconds; null when none did.</summary>
    public double? MedianMs { get; init; }

    public double? P95Ms { get; init; }
}
