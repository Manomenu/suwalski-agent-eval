using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

public sealed record TurnResponse
{
    public required IReadOnlyList<ConversationItem> Items { get; init; }
}
