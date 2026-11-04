using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Evaluation.Runs;

internal static class UndeclaredOutput
{
    public static IReadOnlyList<string> Of(IReadOnlyList<ConversationItem> produced, AgentContract version)
    {
        ArgumentNullException.ThrowIfNull(produced);
        ArgumentNullException.ThrowIfNull(version);
        var agents = version.Agents.ToDictionary(agent => agent.Name, agent => agent.Tools.Select(tool => tool.Name).ToHashSet());
        var anyTool = agents.Values.SelectMany(tools => tools).ToHashSet();
        var events = version.Context.Events.Select(declared => declared.Type).ToHashSet();
        return
        [
            .. produced
                .Select(item =>
                    item switch
                    {
                        ToolCall { Agent: null } call when !anyTool.Contains(call.Name) => $"tool '{call.Name}'",
                        ToolCall { Agent: { } name } when !agents.TryGetValue(name, out var tools) => $"agent '{name}'",
                        ToolCall { Agent: { } name } call when !agents[name].Contains(call.Name) => $"tool '{call.Name}' of agent '{name}'",
                        Event emitted when !events.Contains(emitted.Type) => $"event '{emitted.Type}'",
                        _ => null,
                    }
                )
                .OfType<string>()
                .Distinct(),
        ];
    }
}
