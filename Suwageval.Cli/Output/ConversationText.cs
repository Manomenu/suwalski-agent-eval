using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Cli.Output;

/// <summary>The items of a conversation as the commands print them: a scenario's (<c>scenario</c>) and a monitored one's (<c>monitoring conversation</c>).</summary>
internal static class ConversationText
{
    private const string Indent = "  ";

    /// <summary>One conversation item on one line (a text with several lines continues indented).</summary>
    public static string Item(ConversationItem item) =>
        item switch
        {
            UserMessage message => $"user: {Multiline(message.Text)}",
            AssistantMessage message => $"assistant: {Multiline(message.Text)}",
            ToolCall call => $"tool call {call.Name} {call.Arguments.GetRawText()}",
            ToolResult result => $"tool result{(result.IsError ? " (error)" : "")}: {Multiline(result.Content)}",
            Retrieval retrieval => $"retrieval \"{retrieval.Query}\" ({retrieval.Chunks.Count} chunks)",
            Event @event => $"event {@event.Type} {@event.Data.GetRawText()}",
            _ => item.GetType().Name,
        };

    /// <summary>A text whose later lines are indented under the first.</summary>
    public static string Multiline(string text) =>
        text.ReplaceLineEndings("\n").Replace("\n", "\n" + Indent + Indent, StringComparison.Ordinal);
}
