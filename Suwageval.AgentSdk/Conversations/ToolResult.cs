namespace Suwageval.AgentSdk.Conversations;

public sealed record ToolResult : ConversationItem
{
    /// <summary>The <see cref="ToolCall.Id"/> this answers.</summary>
    public required string CallId { get; init; }

    public required string Content { get; init; }

    public bool IsError { get; init; }
}
