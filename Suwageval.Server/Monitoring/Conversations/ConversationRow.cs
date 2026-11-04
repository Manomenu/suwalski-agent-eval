namespace Suwageval.Server.Monitoring.Conversations;

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
    DateTime? ClosedAt,
    DateTime? CheckedAt = null,
    int FailedChecks = 0,
    int NotJudgedChecks = 0
);
