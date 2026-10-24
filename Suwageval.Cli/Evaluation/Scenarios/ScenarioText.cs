using System.Globalization;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;

namespace Suwageval.Cli.Evaluation.Scenarios;

/// <summary>A scenario as <c>suwageval scenario</c> prints it: header, conversation, checks with their definitions, revisions.</summary>
internal static class ScenarioText
{
    private const string Indent = "  ";

    public static string Describe(ScenarioView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var scenario = view.Scenario.Scenario;
        var text = new StringBuilder();
        text.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{scenario.Key}  revision {view.Scenario.Revision}  tags: {(scenario.Tags.Count == 0 ? "-" : string.Join(", ", scenario.Tags))}"
            )
        );
        text.AppendLine(
            scenario.Samples is { } samples
                ? string.Create(CultureInfo.InvariantCulture, $"samples: {samples}  turns: {scenario.Continuation.Turns}")
                : string.Create(CultureInfo.InvariantCulture, $"turns: {scenario.Continuation.Turns}")
        );
        if (scenario.Continuation.SimulatedUser is { } simulatedUser)
        {
            text.AppendLine("simulated user:");
            foreach (var line in simulatedUser.Trim().Split('\n'))
            {
                text.Append(Indent).AppendLine(line.TrimEnd('\r'));
            }
        }
        if (scenario.Fixtures is { } fixtures)
        {
            text.AppendLine($"fixtures: {fixtures.GetRawText()}");
        }

        text.AppendLine().AppendLine("conversation:");
        foreach (var item in scenario.Conversation)
        {
            text.Append(Indent).AppendLine(Item(item));
        }

        text.AppendLine().AppendLine(string.Create(CultureInfo.InvariantCulture, $"checks ({view.Checks.Count}):"));
        foreach (var detail in view.Checks)
        {
            text.Append(Indent)
                .Append(detail.Check.Key)
                .AppendLine(string.Create(CultureInfo.InvariantCulture, $"  revision {detail.Revision}"));
            foreach (var line in Definition(detail.Check))
            {
                text.Append(Indent).Append(Indent).AppendLine(line);
            }
        }

        text.AppendLine().AppendLine("revisions:");
        foreach (var revision in view.Revisions.OrderByDescending(revision => revision.Revision))
        {
            text.Append(Indent)
                .AppendLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{revision.Revision}  {revision.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z  {revision.ContentHash[..Math.Min(8, revision.ContentHash.Length)]}"
                    )
                );
        }
        return text.ToString().TrimEnd();
    }

    /// <summary>One conversation item on one line (a text with several lines continues indented).</summary>
    internal static string Item(ConversationItem item) =>
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

    /// <summary>The check's definition as YAML-like lines: <c>key: value</c>, a nested object indented, an array as one line of JSON.</summary>
    internal static List<string> Definition(CheckInput check)
    {
        var lines = new List<string>();
        Write(JsonSerializer.SerializeToElement(check, ContractJson.Options), 0, lines, skip: "scenarioKey");
        return lines;
    }

    private static void Write(JsonElement element, int depth, List<string> lines, string? skip = null)
    {
        foreach (var property in element.EnumerateObject().Where(property => property.Name != skip))
        {
            var prefix = new string(' ', depth * 2) + property.Name + ":";
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                lines.Add(prefix);
                Write(property.Value, depth + 1, lines);
            }
            else
            {
                lines.Add($"{prefix} {Scalar(property.Value)}");
            }
        }
    }

    private static string Scalar(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? Multiline(value.GetString() ?? "") : value.GetRawText();

    private static string Multiline(string text) =>
        text.ReplaceLineEndings("\n").Replace("\n", "\n" + Indent + Indent, StringComparison.Ordinal);
}
