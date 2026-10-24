using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Agents;

public sealed record AgentDefinition
{
    public required string Name { get; init; }

    /// <summary>Where suwageval reaches the agent under test (<c>POST {endpointUrl}/suwageval/turn</c>).</summary>
    public required string EndpointUrl { get; init; }

    /// <summary>How many times each scenario's conversation is sent to the agent by default (1 to 20); none means 3.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Samples { get; init; }

    /// <summary>How the agent's regressions are found; none means the defaults.</summary>
    public RegressionSettings? Regression { get; init; }

    /// <summary>Which areas the agent uses; none means evaluation on and monitoring off.</summary>
    public AgentFeatures? Features { get; init; }
}
