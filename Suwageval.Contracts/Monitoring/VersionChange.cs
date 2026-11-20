namespace Suwageval.Contracts.Monitoring;

public sealed record VersionChange(DateTimeOffset At, Guid AgentVersionId, string? Label = null);
