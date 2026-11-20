using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Agents;

public sealed record AgentDefinition
{
    public required string Name { get; init; }

    public required string EndpointUrl { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Samples { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AcGuardrailRetryLimit { get; init; }

    public RegressionSettings? Regression { get; init; }

    public AgentFeatures? Features { get; init; }
}
