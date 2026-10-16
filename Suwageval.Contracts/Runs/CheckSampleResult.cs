using Suwageval.Contracts.Conversations;

namespace Suwageval.Contracts.Runs;

/// <summary>A check's verdict on one sample.</summary>
public sealed record CheckSampleResult
{
    /// <summary>From 1.</summary>
    public required int Sample { get; init; }

    public required bool Passed { get; init; }

    /// <summary>The items that decided the verdict; may be empty (a tool that was not called leaves nothing to show).</summary>
    public required IReadOnlyList<ConversationItem> Evidence { get; init; }

    public required string Message { get; init; }
}
