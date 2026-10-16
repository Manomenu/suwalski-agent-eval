using Suwageval.Contracts.Conversations;

namespace Suwageval.Contracts.Runs;

/// <summary>What the agent produced for one sample of a scenario.</summary>
public sealed record SampleResult
{
    /// <summary>From 1.</summary>
    public required int Sample { get; init; }

    public required SampleStatus Status { get; init; }

    /// <summary>The items the agent produced; none when the sample failed.</summary>
    public IReadOnlyList<ConversationItem>? Output { get; init; }

    public string? Error { get; init; }

    /// <summary>
    /// How long the call to the agent took, measured by Suwageval (wall-clock, whether it answered or not); null when no call was made.
    /// </summary>
    public int? DurationMs { get; init; }

    /// <summary>
    /// Warnings: tool calls and events the agent produced that its version does not declare (<c>tool 'x'</c>, <c>event 'y'</c>).
    /// Not a failure by itself.
    /// </summary>
    public IReadOnlyList<string> Undeclared { get; init; } = [];
}
