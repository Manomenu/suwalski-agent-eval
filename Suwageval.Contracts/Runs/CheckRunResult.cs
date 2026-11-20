using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Runs;

public sealed record CheckRunResult
{
    public required string Key { get; init; }

    public required int Revision { get; init; }

    public required int LatestRevision { get; init; }

    public required CheckInput Definition { get; init; }

    public required bool JudgedByModel { get; init; }

    public string? JudgeModel { get; init; }

    public required bool Passed { get; init; }

    public required double PassRate { get; init; }

    public required double PassThreshold { get; init; }

    public required IReadOnlyList<CheckSampleResult> Samples { get; init; }

    public CheckRegression? Regression { get; init; }

    public required bool PassedFully { get; init; }
}
