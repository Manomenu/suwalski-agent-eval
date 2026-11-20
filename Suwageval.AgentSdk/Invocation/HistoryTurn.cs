using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

public sealed record HistoryTurn
{
    public UserMessage? User { get; init; }

    public required IReadOnlyList<ConversationItem> Items { get; init; }

    public required IReadOnlyList<ToolExchange> ToolExchanges { get; init; }

    public AssistantMessage? Assistant { get; init; }
}
