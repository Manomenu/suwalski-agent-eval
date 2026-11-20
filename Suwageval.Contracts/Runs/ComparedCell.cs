namespace Suwageval.Contracts.Runs;

public sealed record ComparedCell
{
    public required bool Passed { get; init; }

    public required double PassRate { get; init; }

    public required int SamplesPassed { get; init; }

    public required int Samples { get; init; }

    public required int NotJudged { get; init; }

    public required int Revision { get; init; }

    public required int LatestRevision { get; init; }

    public required bool JudgedByModel { get; init; }

    public string? JudgeModel { get; init; }

    public Trend? Trend { get; init; }
}
