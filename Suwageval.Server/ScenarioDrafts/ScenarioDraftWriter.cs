using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Evaluation.Simulation;

namespace Suwageval.Server.ScenarioDrafts;

internal static partial class ScenarioDraftWriter
{
    public const int MaxKeyLength = 60;

    public static ScenarioDraft Draft(
        string conversationId,
        Guid id,
        IReadOnlyList<ConversationItem> items,
        MessageCut? upTo,
        IReadOnlyList<CheckInput> failedChecks,
        (UserDescription User, int Turns)? simulation = null
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(failedChecks);
        var end = upTo is { } cut ? cut.IndexIn(items) ?? throw new ArgumentOutOfRangeException(nameof(upTo)) : items.Count - 1;
        var key = Key(conversationId, id);
        var scenario = new ScenarioInput
        {
            Key = key,
            Conversation = [.. items.Take(end + 1)],
            Continuation = simulation is { } simulated
                ? new Continuation { Turns = simulated.Turns, SimulatedUser = simulated.User.Text }
                : new(),
            Origin = new ScenarioOrigin { MonitoringConversation = id },
        };
        var yaml = new StringBuilder()
            .Append(
                CultureInfo.InvariantCulture,
                $"# From the monitored conversation {conversationId}, copied {(upTo is { } named ? $"up to {named.Describe()}, included" : "to its end")}{(simulation is null ? "" : "; the rest simulated")}.\n"
            )
            .Append(
                simulation is null ? ""
                : simulation.Value.User.Notice is null
                    ? "# The simulated user was described by a model from the whole conversation: review it before you keep it.\n"
                : "# The simulated user is not described yet: rewrite continuation.simulatedUser from the messages it lists.\n"
            )
            .Append("# It ran on real data Suwageval does not keep: add the fixtures the agent needs to answer it the same way.\n")
            .Append(ScenarioYaml.Of(scenario));
        if (failedChecks.Count > 0)
        {
            yaml.Append(
                    "# The monitoring checks this conversation failed, as they are defined (a scenario's checks may need other fields):\n"
                )
                .Append(
                    string.Concat(
                        ContractYaml
                            .Of(new JsonObject { ["checks"] = JsonSerializer.SerializeToNode(failedChecks, ContractJson.Options) })
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
            Notice = simulation?.User.Notice,
        };
    }

    public static string Key(string conversationId, Guid id)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        var words = NotAWordPart().Replace(conversationId.ToLowerInvariant(), "-").Trim('-');
        var key = $"conversation-{(words.Length == 0 ? id.ToString()[..8] : words)}";
        return key.Length <= MaxKeyLength ? key : key[..MaxKeyLength].TrimEnd('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotAWordPart();
}
