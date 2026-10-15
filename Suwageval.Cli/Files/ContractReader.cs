using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;

namespace Suwageval.Cli.Files;

/// <summary>Deserializes a part of a file into a contract record; what the contract rejects becomes a problem at its place in the file.</summary>
internal static partial class ContractReader
{
    public static T? Read<T>(YamlSource source, JsonNode? node, string path, List<Problem> problems)
        where T : class
    {
        try
        {
            var value = node.Deserialize<T>(ContractJson.Options);
            if (value is null)
            {
                problems.Add(source.Problem(path, "A value is required here."));
            }
            return value;
        }
        catch (JsonException e)
        {
            var (relative, message) = Explain(e, typeof(T));
            problems.Add(source.Problem(Join(path, relative), message));
            return null;
        }
        catch (NotSupportedException e) when (e.Message.Contains("type discriminator", StringComparison.Ordinal))
        {
            // System.Text.Json throws this, not a JsonException, when a polymorphic payload has no discriminator.
            problems.Add(
                source.Problem(
                    path,
                    typeof(T) == typeof(CheckInput)
                        ? "A check needs a 'type' (tool-called, tool-not-called, event, text)."
                        : "A conversation item needs a 'kind'."
                )
            );
            return null;
        }
    }

    public static string Join(string path, string relative)
    {
        var rest = relative.TrimStart('$');
        if (rest.Length == 0)
        {
            return path;
        }
        return path.Length == 0 ? rest.TrimStart('.') : path + rest;
    }

    private static (string Path, string Message) Explain(JsonException e, Type target)
    {
        var message = e.Message;
        var at = message.IndexOf(" Path: ", StringComparison.Ordinal);
        if (at >= 0)
        {
            message = message[..at];
        }
        var path = e.Path ?? "$";

        var unknown = UnknownField().Match(message);
        if (unknown.Success)
        {
            var name = unknown.Groups[1].Value;
            return (path.EndsWith(name, StringComparison.Ordinal) ? path : $"{path}.{name}", $"Unknown field '{name}'.");
        }
        var missing = MissingFields().Match(message);
        if (missing.Success)
        {
            var names = string.Join(", ", missing.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries).Select(n => n.Trim('\'')));
            return (path, $"Missing required field: {names}.");
        }
        var discriminator = UnknownDiscriminator().Match(message);
        if (discriminator.Success)
        {
            var value = discriminator.Groups[1].Value;
            return target == typeof(CheckInput)
                ? (path, $"Unknown check type '{value}' (the types: tool-called, tool-not-called, event, text; judge checks come later).")
                : (path, $"Unknown kind '{value}' (the kinds: UserMessage, AssistantMessage, ToolCall, ToolResult, Retrieval, Event).");
        }
        return (path, message);
    }

    [GeneratedRegex("type discriminator id '(.+?)'")]
    private static partial Regex UnknownDiscriminator();

    [GeneratedRegex(@"The JSON property '(.+?)' could not be mapped")]
    private static partial Regex UnknownField();

    [GeneratedRegex(@"missing required properties(?: including)?: (.+?)\.?$")]
    private static partial Regex MissingFields();
}
