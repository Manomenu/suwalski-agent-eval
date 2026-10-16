namespace Suwageval.Contracts.Runs;

/// <summary>Starts a run of an agent's scenarios.</summary>
public sealed record RunRequest
{
    /// <summary>The agent's name or id.</summary>
    public required string Agent { get; init; }

    /// <summary>Only the scenarios with this tag; none means every scenario of the agent.</summary>
    public string? Tag { get; init; }

    /// <summary>Where to reach the agent for this run, and to read its contract from; none means the endpoint the agent was registered with.</summary>
    public string? EndpointUrl { get; init; }
}
