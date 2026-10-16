namespace Suwageval.Contracts.Conversations;

public sealed record AssistantMessage : ConversationItem
{
    public required string Text { get; init; }

    /// <summary>The user cut the answer off before it was finished.</summary>
    public bool? Interrupted { get; init; }

    /// <summary>The ids of the documents the answer is grounded on, as the agent reports them.</summary>
    public IReadOnlyList<string>? Sources { get; init; }
}
