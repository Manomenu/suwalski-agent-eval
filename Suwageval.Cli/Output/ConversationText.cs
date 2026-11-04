using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Cli.Output;

internal static class ConversationText
{
    private const string Indent = "  ";

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

    public static string Multiline(string text) =>
        text.ReplaceLineEndings("\n").Replace("\n", "\n" + Indent + Indent, StringComparison.Ordinal);
}
