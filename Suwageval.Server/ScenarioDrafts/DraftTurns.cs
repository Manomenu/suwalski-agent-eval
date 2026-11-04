using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Server.ScenarioDrafts;

internal static class DraftTurns
{
    public static int After(IReadOnlyList<ConversationItem> items, int end)
    {
        ArgumentNullException.ThrowIfNull(items);
        var left = items.Skip(end + 1).Count(item => item is UserMessage) + (items[end] is UserMessage ? 1 : 0);
        return Math.Min(left, ContinuationRules.MaxTurns);
    }
}
