namespace Suwageval.Contracts.Conversations;

public sealed record AssistantMessage : ConversationItem
{
    public required string Text { get; init; }

    /// <summary>The user cut the answer off before it was finished.</summary>
    public bool? Interrupted { get; init; }
}
