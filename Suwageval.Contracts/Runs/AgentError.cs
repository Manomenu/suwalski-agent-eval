namespace Suwageval.Contracts.Runs;

/// <summary>
/// Samples in which the agent gave no answer (an HTTP error, a timeout, an unreadable answer) for one scenario of one agent version.
/// They do not count against checks; they say the agent itself is unreliable in that scenario.
/// </summary>
public sealed record AgentError
{
    public required string ScenarioKey { get; init; }

    public required Guid AgentVersionId { get; init; }

    /// <summary>The label the version's contract carries, if any.</summary>
    public string? VersionLabel { get; init; }

    /// <summary>The samples without an answer; at least 1.</summary>
    public required int Errors { get; init; }

    /// <summary>All the samples the agent was asked for in the scenario, answered or not.</summary>
    public required int Samples { get; init; }

    /// <summary>The newest run in which the scenario had an error.</summary>
    public required Guid NewestErrorRunId { get; init; }

    /// <summary>The error text of the newest such sample.</summary>
    public required string NewestError { get; init; }
}
