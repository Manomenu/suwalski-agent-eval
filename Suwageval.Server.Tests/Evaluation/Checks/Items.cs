using System.Text.Json;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Tests.Evaluation.Checks;

internal static class Items
{
    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public static ToolCall Call(string name, string argumentsJson = "{}", string? id = null, string? agent = null) =>
        new()
        {
            Id = id ?? $"call-{name}",
            Name = name,
            Arguments = Json(argumentsJson),
            Agent = agent,
        };

    public static ToolResult Result(string callId, string content = "{}") => new() { CallId = callId, Content = content };

    public static Event Emit(string type, string dataJson = "{}") => new() { Type = type, Data = Json(dataJson) };

    public static AssistantMessage Say(string text) => new() { Text = text };

    public static Trace Turns(params ConversationItem[][] turns) => new([.. turns.Select(turn => (IReadOnlyList<ConversationItem>)turn)]);

    public static Trace Turn(params ConversationItem[] items) => Turns(items);
}
