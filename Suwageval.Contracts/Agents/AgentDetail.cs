namespace Suwageval.Contracts.Agents;

public sealed record AgentDetail
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string EndpointUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required int Versions { get; init; }

    public required int Scenarios { get; init; }

    public required int Checks { get; init; }

    public int? Samples { get; init; }

    public int? AcGuardrailRetryLimit { get; init; }

    public RegressionSettings? Regression { get; init; }

    public required AgentFeatures Features { get; init; }
}
