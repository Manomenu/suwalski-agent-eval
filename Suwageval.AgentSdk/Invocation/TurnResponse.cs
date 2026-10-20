using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

/// <summary>
/// What the agent under test answers with: everything it produced in this one turn, in order — its tool calls, their
/// results, its events and its final message.
/// </summary>
public sealed record TurnResponse
{
    public required IReadOnlyList<ConversationItem> Items { get; init; }
}
