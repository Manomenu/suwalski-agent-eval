namespace Suwageval.Server.Agents;

/// <summary>The version, and whether this call created it (<c>false</c>: the same content was registered before).</summary>
internal sealed record AgentVersionRegistration(AgentVersionRow Version, bool Created);
