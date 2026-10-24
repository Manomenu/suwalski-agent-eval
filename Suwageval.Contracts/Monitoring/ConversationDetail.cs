namespace Suwageval.Contracts.Monitoring;

/// <summary>A monitored conversation with its turns, oldest first.</summary>
public sealed record ConversationDetail
{
    public required ConversationSummary Conversation { get; init; }

    public required IReadOnlyList<ConversationTurn> Turns { get; init; }
}
