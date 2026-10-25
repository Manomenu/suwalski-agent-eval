using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Server.ScenarioDrafts;

/// <summary>How many turns a simulation of the rest of a conversation runs by default: as many as the real conversation had after the cut.</summary>
internal static class DraftTurns
{
    /// <summary>
    /// The agent turns left after the item at <paramref name="end"/>: one per user message after it, and one more when it is a user
    /// message itself (the agent answers it first). At most <see cref="ContinuationRules.MaxTurns"/>; 0 when nothing is left.
    /// </summary>
    public static int After(IReadOnlyList<ConversationItem> items, int end)
    {
        ArgumentNullException.ThrowIfNull(items);
        var left = items.Skip(end + 1).Count(item => item is UserMessage) + (items[end] is UserMessage ? 1 : 0);
        return Math.Min(left, ContinuationRules.MaxTurns);
    }
}
