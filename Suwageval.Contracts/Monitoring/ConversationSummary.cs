namespace Suwageval.Contracts.Monitoring;

/// <summary>A conversation of a monitored agent, without its turns.</summary>
public sealed record ConversationSummary
{
    /// <summary>Suwageval's id of the conversation.</summary>
    public required Guid Id { get; init; }

    public required string Agent { get; init; }

    public required string Environment { get; init; }

    /// <summary>The agent's own id of the conversation, as it sent it.</summary>
    public required string ConversationId { get; init; }

    /// <summary>The version that answered its last turn.</summary>
    public required Guid AgentVersionId { get; init; }

    /// <summary>The label of that version's contract, if any.</summary>
    public string? VersionLabel { get; init; }

    /// <summary>True when an earlier turn was answered by another version (the agent was redeployed during the conversation).</summary>
    public bool VersionChanged { get; init; }

    /// <summary>The retrieval data of its last turn, when the agent sends it.</summary>
    public string? DataVersion { get; init; }

    public required ConversationState State { get; init; }

    public required int Turns { get; init; }

    /// <summary>When its first and its last turn ended, by the agent's clock.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset LastTurnAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>When the agent's monitoring checks ran on it (after it closed); none while it was not checked.</summary>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>How many of its checks failed, once checked; a check not judged (the judge's model did not answer) is not counted.</summary>
    public int FailedChecks { get; init; }

    /// <summary>How many of its checks the judge's model did not judge (none configured, no answer), once checked.</summary>
    public int NotJudgedChecks { get; init; }
}
