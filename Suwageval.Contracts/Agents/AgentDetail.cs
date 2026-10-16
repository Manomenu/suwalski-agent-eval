namespace Suwageval.Contracts.Agents;

/// <summary>An agent with what is stored for it: counts of active (not archived) versions, live scenarios and live checks.</summary>
public sealed record AgentDetail
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string EndpointUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required int Versions { get; init; }

    public required int Scenarios { get; init; }

    public required int Checks { get; init; }

    /// <summary>The agent's default number of samples from <c>agent.yaml</c>; none when it has no <c>samples:</c> (3 then).</summary>
    public int? Samples { get; init; }

    /// <summary>The regression settings from <c>agent.yaml</c>; none when it has no <c>regression:</c>.</summary>
    public RegressionSettings? Regression { get; init; }
}
