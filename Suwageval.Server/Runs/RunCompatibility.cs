using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>
/// Whether an agent version can be run against scenarios and checks written for it: every tool call and tool a check names
/// must be a tool of the version, every event a type its context declares, a retrieval needs a version that retrieves and
/// fixtures need a version that declares a fixtures schema. A check of a simulated scenario never picks a turn by its number (a rule of
/// the files, checked here too because the scenario may have become a simulation after the check was pushed).
/// A run that fails these only after the agent was called would waste the call and say nothing useful.
/// </summary>
internal static class RunCompatibility
{
    /// <summary>What does not fit, one sentence each; empty when the version can run it all.</summary>
    public static IReadOnlyList<string> Mismatches(
        AgentContract version,
        IReadOnlyList<(string Key, ScenarioContent Content)> scenarios,
        IReadOnlyList<CheckInput> checks
    )
    {
        ArgumentNullException.ThrowIfNull(version);
        var agents = version.Agents.ToDictionary(agent => agent.Name, agent => agent.Tools.Select(tool => tool.Name).ToHashSet());
        var events = version.Context.Events.Select(declared => declared.Type).ToHashSet();
        var mismatches = new List<string>();

        foreach (var (key, content) in scenarios)
        {
            if (content.Fixtures is not null && version.Context.FixturesSchema is null)
            {
                mismatches.Add($"scenario '{key}': it has fixtures, but the version declares no context.fixturesSchema");
            }
            foreach (var item in content.Conversation)
            {
                switch (item)
                {
                    case ToolCall call when ToolProblem(agents, call.Name, call.Agent) is { } problem:
                        mismatches.Add($"scenario '{key}': the conversation calls tool '{call.Name}'{problem}");
                        break;
                    case Event emitted when !events.Contains(emitted.Type):
                        mismatches.Add(
                            $"scenario '{key}': the conversation has event '{emitted.Type}', which the version does not declare"
                        );
                        break;
                    case Retrieval when !version.Context.UsesRetrieval:
                        mismatches.Add($"scenario '{key}': the conversation has a retrieval, but the version does not use retrieval");
                        break;
                    default:
                        break;
                }
            }
        }

        var continuationOf = scenarios.ToDictionary(scenario => scenario.Key, scenario => scenario.Content.Continuation);
        foreach (var check in checks)
        {
            if (
                continuationOf.TryGetValue(check.ScenarioKey, out var continuation)
                && ContinuationRules.CheckProblem(check, continuation) is { } turnProblem
            )
            {
                mismatches.Add($"check '{check.Key}': {turnProblem}");
            }
            if (CheckRules.NamedTool(check) is { } named && ToolProblem(agents, named.Tool, named.By) is { } problem)
            {
                mismatches.Add($"check '{check.Key}': it names tool '{named.Tool}'{problem}");
            }
            else if (CheckRules.NamedEvent(check) is { } emitted && !events.Contains(emitted))
            {
                mismatches.Add($"check '{check.Key}': it names event '{emitted}', which the version does not declare");
            }
            foreach (var (path, name) in CheckRules.Anchors(check))
            {
                if (!events.Contains(name) && !agents.Values.Any(tools => tools.Contains(name)))
                {
                    mismatches.Add(
                        $"check '{check.Key}': {path} names '{name}', which is neither a tool of the version nor one of its event types"
                    );
                }
            }
        }
        return [.. mismatches.Distinct()];
    }

    /// <summary>Why the tool cannot be had from that agent (or from any agent, when none is named); null when it can.</summary>
    private static string? ToolProblem(Dictionary<string, HashSet<string>> agents, string tool, string? agent)
    {
        if (agent is null)
        {
            return agents.Values.Any(tools => tools.Contains(tool)) ? null : ", which no agent of the version has";
        }
        if (!agents.TryGetValue(agent, out var own))
        {
            return $", by agent '{agent}', which the version does not have";
        }
        return own.Contains(tool) ? null : $", which agent '{agent}' does not have";
    }
}
