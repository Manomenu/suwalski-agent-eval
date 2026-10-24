using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Evaluation.Simulation;

namespace Suwageval.Server.ScenarioDrafts;

/// <summary>
/// A monitored conversation as a scenario file. Taken as it is: every turn before the last is the history, and the last turn's user
/// message is what the agent answers in the scenario. Until a turn N: the turns before N are the history, the agent answers turn N's user
/// message and a simulated user (described from the real one) goes on for as many turns as the real conversation had. What the agent
/// did from that turn on becomes draft checks, as comments: a starting point the person trims, never an expected answer written blindly
/// (the agent may have been wrong, which is often why the conversation was picked).
/// </summary>
internal static partial class ScenarioDraftWriter
{
    /// <summary>How long a proposed key may be.</summary>
    public const int MaxKeyLength = 60;

    /// <param name="conversationId">The agent's own id of the conversation, which the key is proposed from.</param>
    /// <param name="id">Suwageval's id of the conversation: the scenario's origin.</param>
    /// <param name="turns">The conversation's turns, oldest first, each the items the agent recorded.</param>
    /// <param name="untilTurn">Null: taken as it is. A turn (from 1, before the last): replayed up to it, the rest simulated.</param>
    /// <param name="simulatedUser">The simulated user's description; required with <paramref name="untilTurn"/>.</param>
    public static ScenarioDraft Draft(
        string conversationId,
        Guid id,
        IReadOnlyList<IReadOnlyList<ConversationItem>> turns,
        int? untilTurn = null,
        UserDescription? simulatedUser = null
    )
    {
        ArgumentNullException.ThrowIfNull(turns);
        var simulated = untilTurn is { } until && until < turns.Count;
        var at = simulated ? untilTurn!.Value - 1 : turns.Count - 1;
        var opening = at < 0 ? [] : turns[at];
        var lastUser = opening.Select((item, index) => (item, index)).LastOrDefault(pair => pair.item is UserMessage).index;
        var conversation = turns.Take(Math.Max(at, 0)).SelectMany(turn => turn).Concat(opening.Take(lastUser + 1)).ToList();
        // What the agent did from the opening turn on; in a simulation the later turns too, without the real user's messages.
        List<ConversationItem> produced =
        [
            .. opening.Skip(lastUser + 1),
            .. turns.Skip(at + 1).SelectMany(turn => turn).Where(item => item is not UserMessage),
        ];
        var key = Key(conversationId, id);
        var scenario = new ScenarioInput
        {
            Key = key,
            Conversation = conversation,
            Continuation = simulated
                ? new Continuation
                {
                    Turns = Math.Min(turns.Count - at, ContinuationRules.MaxTurns),
                    SimulatedUser = (simulatedUser ?? throw new ArgumentNullException(nameof(simulatedUser))).Text,
                }
                : new Continuation(),
            Origin = new ScenarioOrigin { MonitoringConversation = id },
        };
        var header = simulated
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"# From the monitored conversation {conversationId}, replayed to turn {untilTurn}: the agent answers its message, then a simulated user goes on.\n# Review simulatedUser: it was drafted from the real user's messages.\n"
            )
            : $"# From the monitored conversation {conversationId}, taken as it is: the agent answers its last message.\n";
        var yaml = new StringBuilder()
            .Append(header)
            .Append("# It ran on real data Suwageval does not keep: add the fixtures the agent needs to answer it the same way.\n")
            .Append(ScenarioYaml.Of(scenario));
        var checks = DraftChecks(produced);
        if (checks.Count > 0)
        {
            yaml.Append(
                    simulated
                        ? "# Draft checks from what the agent did from that turn on in the real conversation: in a simulation they look at the whole conversation; keep what it must do, delete the rest.\n"
                        : "# Draft checks from what the agent did in that last turn: keep what it must do, delete the rest, never just the answer it gave.\n"
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
            Notice = simulated ? simulatedUser?.Notice : null,
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
