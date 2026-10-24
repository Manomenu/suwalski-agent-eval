namespace Suwageval.Contracts.Monitoring;

/// <summary>
/// What the Monitoring page draws for an agent, day by day (UTC) over the last days: the conversations and how many were checked, each
/// check's verdicts per version that answered, and the days a version or the retrieval data changed.
/// </summary>
public sealed record MonitoringTrend
{
    public required IReadOnlyList<MonitoringDay> Days { get; init; }

    public required IReadOnlyList<MonitoringPoint> Points { get; init; }

    /// <summary>When a version first answered in the period, oldest first: the deploys.</summary>
    public required IReadOnlyList<VersionChange> Versions { get; init; }

    /// <summary>When a data version was first seen in the period, oldest first (only for an agent that sends it).</summary>
    public required IReadOnlyList<DataChange> DataVersions { get; init; }
}
