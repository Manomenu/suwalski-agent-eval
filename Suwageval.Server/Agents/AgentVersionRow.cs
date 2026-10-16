namespace Suwageval.Server.Agents;

/// <summary>A row of <c>agent_versions</c>; <see cref="Content"/> is the definition as the agent sent it.</summary>
internal sealed record AgentVersionRow(
    Guid Id,
    Guid AgentId,
    string Content,
    string ContentHash,
    string? Label,
    DateTime CreatedAt,
    DateTime? ArchivedAt
);
