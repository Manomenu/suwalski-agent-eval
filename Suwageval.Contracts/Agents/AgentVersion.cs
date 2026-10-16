namespace Suwageval.Contracts.Agents;

/// <summary>A registered version of an agent, without its content.</summary>
public sealed record AgentVersion(Guid Id, Guid AgentId, string? Label, string ContentHash, DateTimeOffset CreatedAt);
