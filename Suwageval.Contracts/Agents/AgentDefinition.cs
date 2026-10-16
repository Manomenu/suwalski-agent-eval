namespace Suwageval.Contracts.Agents;

public sealed record AgentDefinition
{
    public required string Name { get; init; }

    /// <summary>Where suwageval reaches the agent under test (<c>POST {endpointUrl}/suwageval/turn</c>).</summary>
    public required string EndpointUrl { get; init; }

    /// <summary>How the agent's regressions are found; none means the defaults.</summary>
    public RegressionSettings? Regression { get; init; }
}
