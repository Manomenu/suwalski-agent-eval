namespace Suwageval.Contracts.Runs;

public sealed record CheckRegression
{
    public required RegressionState State { get; init; }

    public Guid? FailingSince { get; init; }

    public Guid? LastPassedRun { get; init; }

    public bool DataChanged { get; init; }

    public IReadOnlyList<CheckHistoryPoint> History { get; init; } = [];
}
