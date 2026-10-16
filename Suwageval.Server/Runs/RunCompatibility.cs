using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;

namespace Suwageval.Server.Runs;

/// <summary>
/// Whether an agent version can be run against scenarios and checks written for it: every tool call and tool a check names
/// must be a tool of the version, every event a type its context declares, and a retrieval needs a version that retrieves.
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
        var tools = version.Tools.Select(tool => tool.Name).ToHashSet();
        var events = version.Context.Events.Select(declared => declared.Type).ToHashSet();
        var mismatches = new List<string>();

        foreach (var (key, content) in scenarios)
        {
            foreach (var item in content.Conversation)
            {
                switch (item)
                {
                    case ToolCall call when !tools.Contains(call.Name):
                        mismatches.Add($"scenario '{key}': the conversation calls tool '{call.Name}', which the version does not have");
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
            var tool = check switch
            {
                ToolCalledCheck called => called.Tool,
                ToolNotCalledCheck notCalled => notCalled.Tool,
                _ => null,
            };
            if (tool is not null && !tools.Contains(tool))
            {
                mismatches.Add($"check '{check.Key}': it names tool '{tool}', which the version does not have");
            }
            else if (check is EventCheck emitted && !events.Contains(emitted.Event))
            {
                mismatches.Add($"check '{check.Key}': it names event '{emitted.Event}', which the version does not declare");
            }
        }
        return [.. mismatches.Distinct()];
    }
}
