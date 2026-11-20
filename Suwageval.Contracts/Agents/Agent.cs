namespace Suwageval.Contracts.Agents;

public sealed record Agent
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string EndpointUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
