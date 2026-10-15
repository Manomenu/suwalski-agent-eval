namespace Suwageval.Contracts.Agents;

public sealed record AgentDefinition
{
    public required string Name { get; init; }

    /// <summary>Where suwageval reaches the agent under test (<c>POST {endpointUrl}/suwageval/turn</c>).</summary>
    public required string EndpointUrl { get; init; }
}
