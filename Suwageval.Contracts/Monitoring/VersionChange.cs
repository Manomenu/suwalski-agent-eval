namespace Suwageval.Contracts.Monitoring;

/// <summary>When a version first answered a monitored conversation in a period: a deploy.</summary>
public sealed record VersionChange(DateTimeOffset At, Guid AgentVersionId, string? Label = null);
