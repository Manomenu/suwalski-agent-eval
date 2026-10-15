namespace Suwageval.Server.Agents;

/// <summary>The agent, and whether this call created it (<c>false</c>: the name was taken and this is the existing one).</summary>
internal sealed record AgentCreation(AgentRow Agent, bool Created);
