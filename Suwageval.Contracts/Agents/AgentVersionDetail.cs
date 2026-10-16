namespace Suwageval.Contracts.Agents;

/// <summary>A registered version of an agent with its content (<c>Content.Label</c> is the version's label).</summary>
public sealed record AgentVersionDetail(Guid Id, Guid AgentId, string ContentHash, DateTimeOffset CreatedAt, AgentVersionInput Content);
