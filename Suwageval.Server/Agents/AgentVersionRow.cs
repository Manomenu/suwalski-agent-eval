namespace Suwageval.Server.Agents;

internal sealed record AgentVersionRow(
    Guid Id,
    Guid AgentId,
    string Content,
    string ContentHash,
    string? Label,
    DateTime CreatedAt,
    DateTime? ArchivedAt
);
