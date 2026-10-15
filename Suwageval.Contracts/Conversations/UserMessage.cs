namespace Suwageval.Contracts.Conversations;

public sealed record UserMessage : ConversationItem
{
    public required string Text { get; init; }
}
