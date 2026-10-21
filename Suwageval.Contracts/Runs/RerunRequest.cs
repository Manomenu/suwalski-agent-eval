namespace Suwageval.Contracts.Runs;

/// <summary>The optional body of a rerun: everything else (agent, tags) is taken from the run being repeated.</summary>
public sealed record RerunRequest
{
    /// <summary>Where to reach the agent for the new run; none means the endpoint the repeated run used.</summary>
    public string? EndpointUrl { get; init; }

    /// <summary>The group of the new run (<see cref="RunGroupRules"/>); none means no group, the repeated run's group is not inherited.</summary>
    public string? Group { get; init; }
}
