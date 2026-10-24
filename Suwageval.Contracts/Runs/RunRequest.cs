namespace Suwageval.Contracts.Runs;

/// <summary>Starts a run of an agent's scenarios.</summary>
public sealed record RunRequest
{
    /// <summary>The agent's name or id.</summary>
    public required string Agent { get; init; }

    /// <summary>Only the scenarios with any of these tags; empty means every scenario of the agent.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Where to reach the agent for this run, and to read its contract from; none means the endpoint the agent was registered with.</summary>
    public string? EndpointUrl { get; init; }

    /// <summary>The group the run belongs to (<see cref="RunGroupRules"/>); none means no group.</summary>
    public string? Group { get; init; }

    /// <summary>Why the run is started (<see cref="NoteRules"/>); none means no note. It can be changed after the run.</summary>
    public string? Note { get; init; }

    /// <summary>Where the agent's repository stood; none when the run does not come from a git repository (it is then compared with every run of the agent).</summary>
    public RunOrigin? Origin { get; init; }
}
