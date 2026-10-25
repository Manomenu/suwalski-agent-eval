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

/// <summary>
/// A monitored conversation as a scenario file, copied to its end or up to a chosen message, the rest optionally simulated: the base a
/// person (or Claude) builds a scenario on. The monitoring checks the conversation failed come below as comments, with their definitions: a monitoring check is not
/// always a valid scenario check as it is, so it is never written as one.
/// </summary>
internal static partial class ScenarioDraftWriter
{
    /// <summary>How long a proposed key may be.</summary>
    public const int MaxKeyLength = 60;

    /// <param name="conversationId">The agent's own id of the conversation, which the key is proposed from.</param>
    /// <param name="id">Suwageval's id of the conversation: the scenario's origin.</param>
    /// <param name="items">The conversation's items, oldest first, over all its turns.</param>
    /// <param name="upTo">Where the copy ends: null, the whole conversation; else at that message, included.</param>
    /// <param name="failedChecks">The monitoring checks the conversation failed, as they are defined.</param>
    /// <param name="simulation">The rest simulated (only with <paramref name="upTo"/>): the user described from the whole conversation and the turns; null, copied only.</param>
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

    /// <summary>The key proposed from the agent's id of the conversation (lowercase words joined by hyphens), else from Suwageval's.</summary>
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
