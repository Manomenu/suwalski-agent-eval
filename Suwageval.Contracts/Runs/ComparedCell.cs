namespace Suwageval.Contracts.Runs;

/// <summary>What one run's scenarios say about one check.</summary>
public sealed record ComparedCell
{
    /// <summary>The pass rate reached the threshold.</summary>
    public required bool Passed { get; init; }

    /// <summary>The share of samples that passed, from 0 to 1.</summary>
    public required double PassRate { get; init; }

    /// <summary>Samples that passed.</summary>
    public required int SamplesPassed { get; init; }

    public required int Samples { get; init; }

    /// <summary>Samples the judge could not judge (counted as failed, but not a verdict).</summary>
    public required int NotJudged { get; init; }

    public required int Revision { get; init; }

    public required int LatestRevision { get; init; }

    public required bool JudgedByModel { get; init; }

    /// <summary>The model that judged the check in this run; absent for a deterministic check and for a run from before the name was recorded.</summary>
    public string? JudgeModel { get; init; }

    /// <summary>Against the baseline's cell; absent when either run has no such check.</summary>
    public Trend? Trend { get; init; }
}
