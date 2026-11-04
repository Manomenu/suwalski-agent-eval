namespace Suwageval.Server.Monitoring.Conversations;

internal sealed record TurnRow(Guid Id, DateTime At, Guid AgentVersionId, string? DataVersion, string Items);
