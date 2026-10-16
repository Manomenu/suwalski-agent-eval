namespace Suwageval.Contracts.Runs;

/// <summary>The optional body of a rerun: everything else (agent, tag) is taken from the run being repeated.</summary>
public sealed record RerunRequest
{
    /// <summary>Where to reach the agent for the new run; none means the endpoint the repeated run used.</summary>
    public string? EndpointUrl { get; init; }
}
