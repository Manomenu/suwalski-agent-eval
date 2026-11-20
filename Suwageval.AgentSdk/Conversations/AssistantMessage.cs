namespace Suwageval.AgentSdk.Conversations;

public sealed record AssistantMessage : ConversationItem
{
    public required string Text { get; init; }

    public bool? Interrupted { get; init; }

    public IReadOnlyList<string>? Sources { get; init; }
}
