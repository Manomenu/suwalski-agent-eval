using System.Text.Json.Nodes;

namespace Suwageval.Cli.Files;

/// <summary>
/// The one place where the log shorthands of a scenario file become the contract's full form:
/// <c>- user: "…"</c>, <c>- assistant: "…"</c>, <c>- tool_call: {…}</c>, <c>- tool_result: {…}</c>, <c>- event: {…}</c>.
/// An item with <c>kind:</c> is already the full form and passes through.
/// </summary>
internal static class ConversationShorthands
{
    private const string Expected =
        "A conversation item is one of 'user', 'assistant', 'tool_call', 'tool_result', 'event' (for example '- user: \"Hello\"'), "
        + "or the full form with 'kind'.";

    private static readonly Dictionary<string, string> Kinds = new()
    {
        ["user"] = "UserMessage",
        ["assistant"] = "AssistantMessage",
        ["tool_call"] = "ToolCall",
        ["tool_result"] = "ToolResult",
        ["event"] = "Event",
    };

    /// <summary>The item in the contract's full form, or null (with a problem) when it is neither form.</summary>
    public static JsonNode? Expand(JsonNode? item, string path, YamlSource source, List<Problem> problems)
    {
        if (item is not JsonObject obj)
        {
            problems.Add(source.Problem(path, Expected));
            return null;
        }
        if (obj.ContainsKey("kind"))
        {
            return obj.DeepClone();
        }
        if (obj.Count != 1 || !Kinds.TryGetValue(obj.First().Key, out var kind))
        {
            problems.Add(source.Problem(path, Expected));
            return null;
        }

        var (shorthand, body) = obj.First();
        if (shorthand is "user" or "assistant")
        {
            if (body is not JsonValue value || !value.TryGetValue<string>(out var text))
            {
                problems.Add(source.Problem(YamlSource.Child(path, shorthand), $"'{shorthand}' is the message's text, in quotes."));
                return null;
            }
            source.AddAlias(YamlSource.Child(path, "text"), YamlSource.Child(path, shorthand));
            return new JsonObject { ["kind"] = kind, ["text"] = text };
        }

        if (body is not JsonObject fields)
        {
            problems.Add(source.Problem(YamlSource.Child(path, shorthand), $"'{shorthand}' holds the item's fields as a mapping."));
            return null;
        }
        var expanded = new JsonObject { ["kind"] = kind };
        foreach (var (name, value) in fields)
        {
            expanded[shorthand == "tool_result" && name == "call_id" ? "callId" : name] = value?.DeepClone();
        }
        source.AddAlias(path, YamlSource.Child(path, shorthand));
        if (shorthand == "tool_result")
        {
            source.AddAlias(YamlSource.Child(path, "callId"), YamlSource.Child(YamlSource.Child(path, shorthand), "call_id"));
        }
        return expanded;
    }
}
