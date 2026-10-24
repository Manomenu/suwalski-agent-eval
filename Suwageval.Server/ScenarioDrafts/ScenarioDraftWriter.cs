using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.ScenarioDrafts;

/// <summary>
/// A monitored conversation as a scenario file, taken as it is: every turn before the last is the history, and the last turn's user
/// message is what the agent answers in the scenario. What the agent did in that last turn becomes draft checks, as comments: a
/// starting point the person trims, never an expected answer written blindly (the agent may have been wrong, which is often why the
/// conversation was picked).
/// </summary>
internal static partial class ScenarioDraftWriter
{
    /// <summary>How long a proposed key may be.</summary>
    public const int MaxKeyLength = 60;

    /// <param name="conversationId">The agent's own id of the conversation, which the key is proposed from.</param>
    /// <param name="id">Suwageval's id of the conversation: the scenario's origin.</param>
    /// <param name="turns">The conversation's turns, oldest first, each the items the agent recorded.</param>
    public static ScenarioDraft Draft(string conversationId, Guid id, IReadOnlyList<IReadOnlyList<ConversationItem>> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        var last = turns.Count == 0 ? [] : turns[^1];
        var lastUser = last.Select((item, index) => (item, index)).LastOrDefault(pair => pair.item is UserMessage).index;
        var conversation = turns.Take(turns.Count - 1).SelectMany(turn => turn).Concat(last.Take(lastUser + 1)).ToList();
        var key = Key(conversationId, id);
        var scenario = new ScenarioInput
        {
            Key = key,
            Conversation = conversation,
            Origin = new ScenarioOrigin { MonitoringConversation = id },
        };
        var yaml = new StringBuilder()
            .Append(
                CultureInfo.InvariantCulture,
                $"# From the monitored conversation {conversationId}, taken as it is: the agent answers its last message."
            )
            .Append('\n')
            .Append("# It ran on real data Suwageval does not keep: add the fixtures the agent needs to answer it the same way.\n")
            .Append(ScenarioYaml.Of(scenario));
        var checks = DraftChecks([.. last.Skip(lastUser + 1)]);
        if (checks.Count > 0)
        {
            yaml.Append(
                    "# Draft checks from what the agent did in that last turn: keep what it must do, delete the rest, never just the answer it gave.\n"
                )
                .Append(
                    string.Concat(
                        ContractYaml
                            .Of(new JsonObject { ["checks"] = checks })
                            .Split('\n')
                            .Where(line => line.Length > 0)
                            .Select(line => $"# {line}\n")
                    )
                );
        }
        return new ScenarioDraft
        {
            Key = key,
            Path = $"suwageval/scenarios/{key}.yaml",
            Yaml = yaml.ToString(),
        };
    }

    /// <summary>The key proposed from the agent's id of the conversation (lowercase words joined by hyphens), else from Suwageval's.</summary>
    public static string Key(string conversationId, Guid id)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        var words = NotAWordPart().Replace(conversationId.ToLowerInvariant(), "-").Trim('-');
        var key = $"conversation-{(words.Length == 0 ? id.ToString()[..8] : words)}";
        return key.Length <= MaxKeyLength ? key : key[..MaxKeyLength].TrimEnd('-');
    }

    /// <summary>A check per tool call, event and cited source of the agent's answer, in the order they came.</summary>
    private static JsonArray DraftChecks(IReadOnlyList<ConversationItem> produced)
    {
        var checks = new JsonArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string Unique(string key)
        {
            var unique = key;
            for (var n = 2; !keys.Add(unique); n++)
            {
                unique = $"{key}-{n}";
            }
            return unique;
        }
        foreach (var item in produced)
        {
            switch (item)
            {
                case ToolCall call:
                    var toolCheck = new JsonObject
                    {
                        ["key"] = Unique($"calls-{Slug(call.Name)}"),
                        ["type"] = "tool-called",
                        ["tool"] = call.Name,
                    };
                    if (call.Agent is { } agent)
                    {
                        toolCheck["by"] = agent;
                    }
                    toolCheck["args"] = JsonNode.Parse(call.Arguments.GetRawText());
                    checks.Add(toolCheck);
                    break;
                case Event @event:
                    checks.Add(
                        new JsonObject
                        {
                            ["key"] = Unique($"emits-{Slug(@event.Type)}"),
                            ["type"] = "event",
                            ["event"] = @event.Type,
                            ["data"] = JsonNode.Parse(@event.Data.GetRawText()),
                        }
                    );
                    break;
                case AssistantMessage { Sources.Count: > 0 } answer:
                    checks.Add(
                        new JsonObject
                        {
                            ["key"] = Unique("cites-its-sources"),
                            ["type"] = "cited",
                            ["sources"] = new JsonArray([.. answer.Sources.Select(source => (JsonNode)source)]),
                        }
                    );
                    break;
                default:
                    break;
            }
        }
        return checks;
    }

    private static string Slug(string name) => NotAWordPart().Replace(name.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotAWordPart();
}
