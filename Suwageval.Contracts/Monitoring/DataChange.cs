namespace Suwageval.Contracts.Monitoring;

/// <summary>When a data version was first seen on a monitored turn in a period.</summary>
public sealed record DataChange(DateTimeOffset At, string DataVersion);
