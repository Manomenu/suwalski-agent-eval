namespace Suwageval.Server.Monitoring.Conversations;

/// <summary>A row of <c>monitoring_turns</c>; <see cref="Items"/> is the JSON array of its conversation items.</summary>
internal sealed record TurnRow(Guid Id, DateTime At, Guid AgentVersionId, string? DataVersion, string Items);
