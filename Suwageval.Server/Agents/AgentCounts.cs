namespace Suwageval.Server.Agents;

/// <summary>What is stored for an agent: versions, live scenarios, live checks.</summary>
internal sealed record AgentCounts(int Versions, int Scenarios, int Checks);
