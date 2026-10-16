namespace Suwageval.Contracts.Agents;

/// <summary>An agent under test. The request that creates it is <see cref="AgentDefinition"/>.</summary>
public sealed record Agent
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string EndpointUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
