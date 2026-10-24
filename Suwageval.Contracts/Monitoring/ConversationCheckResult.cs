using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Contracts.Monitoring;

/// <summary>The verdict of one monitoring check on one conversation, with the items that decided it.</summary>
public sealed record ConversationCheckResult
{
    public required string Key { get; init; }

    public required int Revision { get; init; }

    public required bool Passed { get; init; }

    /// <summary>The check needed the judge's model and none could answer: it failed without a verdict on the agent.</summary>
    public bool NotJudged { get; init; }

    public required string Message { get; init; }

    public IReadOnlyList<ConversationItem> Evidence { get; init; } = [];

    /// <summary>The model that gave the verdict, for a check judged by one.</summary>
    public string? JudgeModel { get; init; }
}
