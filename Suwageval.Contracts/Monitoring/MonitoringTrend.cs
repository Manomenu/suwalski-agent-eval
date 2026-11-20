namespace Suwageval.Contracts.Monitoring;

public sealed record MonitoringTrend
{
    public required IReadOnlyList<MonitoringDay> Days { get; init; }

    public required IReadOnlyList<MonitoringPoint> Points { get; init; }

    public required IReadOnlyList<VersionChange> Versions { get; init; }

    public required IReadOnlyList<DataChange> DataVersions { get; init; }

    public IReadOnlyList<string> Environments { get; init; } = [];
}
