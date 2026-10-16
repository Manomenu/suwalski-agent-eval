using Suwageval.Contracts.Conversations;

namespace Suwageval.Contracts.Runs;

/// <summary>A check's verdict on one sample.</summary>
public sealed record CheckSampleResult
{
    /// <summary>From 1.</summary>
    public required int Sample { get; init; }

    /// <summary>False also when <c>notJudged</c>: a sample nobody could judge does not pass.</summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// True when a judge check could not get a verdict (the model was unreachable, too slow or answered unreadably) — told
    /// apart from a verdict of "failed"; <c>message</c> says what happened.
    /// </summary>
    public required bool NotJudged { get; init; }

    /// <summary>The items that decided the verdict; may be empty (a tool that was not called leaves nothing to show).</summary>
    public required IReadOnlyList<ConversationItem> Evidence { get; init; }

    public required string Message { get; init; }
}
