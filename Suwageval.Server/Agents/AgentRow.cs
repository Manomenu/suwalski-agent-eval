namespace Suwageval.Server.Agents;

/// <summary>A row of <c>agents</c>.</summary>
internal sealed record AgentRow(Guid Id, string Name, string EndpointUrl, DateTime CreatedAt);
