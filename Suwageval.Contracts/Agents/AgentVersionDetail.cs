using Suwageval.AgentSdk.Agents;

namespace Suwageval.Contracts.Agents;

public sealed record AgentVersionDetail(
    Guid Id,
    Guid AgentId,
    string ContentHash,
    DateTimeOffset CreatedAt,
    AgentContract Content,
    DateTimeOffset? ArchivedAt = null
);
