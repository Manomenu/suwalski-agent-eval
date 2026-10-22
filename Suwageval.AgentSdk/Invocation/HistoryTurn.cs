using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

/// <summary>One earlier turn of the conversation: a user message and everything the agent did until its next one.</summary>
public sealed record HistoryTurn
{
    public required UserMessage User { get; init; }

    /// <summary>Everything between the user message and the assistant message, as logged and in order: tool calls and results, events, retrievals.</summary>
    public required IReadOnlyList<ConversationItem> Items { get; init; }

    /// <summary>The tool calls of <see cref="Items"/> with their results, in the order of the results.</summary>
    public required IReadOnlyList<ToolExchange> ToolExchanges { get; init; }

    /// <summary>Null when the user wrote again before the agent answered.</summary>
    public AssistantMessage? Assistant { get; init; }
}
