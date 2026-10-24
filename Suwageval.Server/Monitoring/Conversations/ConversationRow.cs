namespace Suwageval.Server.Monitoring.Conversations;

/// <summary>A row of <c>monitoring_conversations</c> with its agent's name and its version's label.</summary>
internal sealed record ConversationRow(
    Guid Id,
    string AgentName,
    string Environment,
    string ConversationKey,
    Guid AgentVersionId,
    string? VersionLabel,
    bool VersionChanged,
    string? DataVersion,
    string State,
    int Turns,
    DateTime StartedAt,
    DateTime LastTurnAt,
    DateTime? ClosedAt
);
