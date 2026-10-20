using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Contracts.Runs;

/// <summary>What the agent produced for one sample of a scenario.</summary>
public sealed record SampleResult
{
    /// <summary>From 1.</summary>
    public required int Sample { get; init; }

    public required SampleStatus Status { get; init; }

    /// <summary>
    /// The items the agent produced. In a simulation, every turn in order, each turn but one the agent started opening with the simulated
    /// user's <c>UserMessage</c> (the agent never produces one); a simulation that stopped keeps what happened before. None when a sample
    /// of one turn failed.
    /// </summary>
    public IReadOnlyList<ConversationItem>? Output { get; init; }

    public string? Error { get; init; }

    /// <summary>
    /// How long the call to the agent took, measured by Suwageval (wall-clock, whether it answered or not); null when no call was made.
    /// </summary>
    public int? DurationMs { get; init; }

    /// <summary>
    /// In a simulation, how long the agent took for each turn, in order (their sum is <c>durationMs</c>; the simulated user's time
    /// is not in it); null for a scenario of one turn.
    /// </summary>
    public IReadOnlyList<int>? TurnDurationsMs { get; init; }

    /// <summary>In a simulation, whether the simulated user ended the conversation before the scenario's last turn.</summary>
    public bool EndedByUser { get; init; }

    /// <summary>
    /// Warnings: tool calls and events the agent produced that its version does not declare (<c>tool 'x'</c>, <c>event 'y'</c>).
    /// Not a failure by itself.
    /// </summary>
    public IReadOnlyList<string> Undeclared { get; init; } = [];
}
