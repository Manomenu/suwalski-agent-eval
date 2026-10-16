using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;

namespace Suwageval.Server.Runs;

/// <summary>
/// Whether an agent version can be run against scenarios and checks written for it: every tool call and tool a check names
/// must be a tool of the version, every event a type its context declares, a retrieval needs a version that retrieves and
/// fixtures need a version that declares a fixtures schema.
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

        foreach (var check in checks)
        {
            var (tool, by) = check switch
            {
                ToolCalledCheck called => (called.Tool, called.By),
                ToolNotCalledCheck notCalled => (notCalled.Tool, notCalled.By),
                _ => (null, null),
            };
            if (tool is not null && ToolProblem(agents, tool, by) is { } problem)
            {
                mismatches.Add($"check '{check.Key}': it names tool '{tool}'{problem}");
            }
            else if (EventOf(check) is { } emitted && !events.Contains(emitted))
            {
                mismatches.Add($"check '{check.Key}': it names event '{emitted}', which the version does not declare");
            }
        }
        return [.. mismatches.Distinct()];
    }

    private static string? EventOf(CheckInput check) =>
        check switch
        {
            EventCheck emitted => emitted.Event,
            EventNotEmittedCheck notEmitted => notEmitted.Event,
            _ => null,
        };

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
