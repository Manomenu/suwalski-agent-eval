namespace Suwageval.Contracts.Agents;

/// <summary>A registered version of an agent, without its content; <c>ArchivedAt</c> is set while the version is archived.</summary>
public sealed record AgentVersion(
    Guid Id,
    Guid AgentId,
    string? Label,
    string ContentHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt
);
