namespace Suwageval.AgentSdk.Conversations;

public sealed record UserMessage : ConversationItem
{
    public required string Text { get; init; }
}
