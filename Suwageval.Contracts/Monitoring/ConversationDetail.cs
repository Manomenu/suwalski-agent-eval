namespace Suwageval.Contracts.Monitoring;

public sealed record ConversationDetail
{
    public required ConversationSummary Conversation { get; init; }

    public required IReadOnlyList<ConversationTurn> Turns { get; init; }

    public IReadOnlyList<ConversationCheckResult> Checks { get; init; } = [];
}
