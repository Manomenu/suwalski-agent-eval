using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

internal static class HistoryTurnSplitter
{
    public static IReadOnlyList<HistoryTurn> Split(IEnumerable<ConversationItem> history)
    {
        var turns = new List<HistoryTurn>();
        var callsById = new Dictionary<string, ToolCall>();
        UserMessage? user = null;
        var open = false;
        var items = new List<ConversationItem>();
        var exchanges = new List<ToolExchange>();

        void Close(AssistantMessage? assistant)
        {
            if (!open)
            {
                return;
            }
            if (assistant is null && exchanges.Count > 0)
            {
                throw new TurnRejectedException("Tool calls in the conversation must be followed by the assistant message they led to.");
            }
            turns.Add(
                new HistoryTurn
                {
                    User = user,
                    Items = [.. items],
                    ToolExchanges = [.. exchanges],
                    Assistant = assistant,
                }
            );
            user = null;
            open = false;
            items.Clear();
            exchanges.Clear();
        }

        foreach (var item in history)
        {
            switch (item)
            {
                case UserMessage userMessage:
                    Close(null);
                    user = userMessage;
                    open = true;
                    break;
                case AssistantMessage assistantMessage:
                    open = true;
                    Close(assistantMessage);
                    break;
                default:
                    open = true;
                    Add(item, callsById, items, exchanges);
                    break;
            }
        }
        Close(null);
        return turns;
    }

    private static void Add(
        ConversationItem item,
        Dictionary<string, ToolCall> callsById,
        List<ConversationItem> items,
        List<ToolExchange> exchanges
    )
    {
        if (item is ToolCall call && !callsById.TryAdd(call.Id, call))
        {
            throw new TurnRejectedException($"Tool call id '{call.Id}' appears twice in the conversation.");
        }
        if (item is ToolResult result)
        {
            if (!callsById.TryGetValue(result.CallId, out var answered))
            {
                throw new TurnRejectedException($"Tool result for call '{result.CallId}' has no tool call before it.");
            }
            exchanges.Add(new ToolExchange(answered, result));
        }
        items.Add(item);
    }
}
