namespace Suwageval.Contracts.Runs;

/// <summary>A run with what every scenario produced and how every check judged it.</summary>
public sealed record RunResults
{
    public required RunSummary Run { get; init; }

    public required IReadOnlyList<ScenarioRunResult> Scenarios { get; init; }

    /// <summary>The median turn time over all samples that made a call, in milliseconds; null when none did.</summary>
    public double? MedianMs { get; init; }

    /// <summary>The 95th percentile (nearest rank) of the same turn times, in milliseconds; null when none did.</summary>
    public double? P95Ms { get; init; }
}
