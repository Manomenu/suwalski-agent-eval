using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Conversations;

namespace Suwageval.Server.Runs;

/// <summary>
/// What an agent produced beyond what its version declares: a tool call to a tool the version does not have, an event of a
/// type it does not declare. A warning on the sample, never a failure by itself — the agent may simply have changed.
/// </summary>
internal static class UndeclaredOutput
{
    /// <summary>One sentence per distinct tool and event type, in the order they first appear.</summary>
    public static IReadOnlyList<string> Of(IReadOnlyList<ConversationItem> produced, AgentContract version)
    {
        ArgumentNullException.ThrowIfNull(produced);
        ArgumentNullException.ThrowIfNull(version);
        var tools = version.Tools.Select(tool => tool.Name).ToHashSet();
        var events = version.Context.Events.Select(declared => declared.Type).ToHashSet();
        return
        [
            .. produced
                .Select(item =>
                    item switch
                    {
                        ToolCall call when !tools.Contains(call.Name) => $"tool '{call.Name}'",
                        Event emitted when !events.Contains(emitted.Type) => $"event '{emitted.Type}'",
                        _ => null,
                    }
                )
                .OfType<string>()
                .Distinct(),
        ];
    }
}
