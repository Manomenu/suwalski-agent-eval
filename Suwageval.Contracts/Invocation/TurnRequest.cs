using Suwageval.Contracts.Conversations;

namespace Suwageval.Contracts.Invocation;

/// <summary>What suwageval sends the agent under test: the conversation so far, whose last item the agent answers.</summary>
public sealed record TurnRequest
{
    public required IReadOnlyList<ConversationItem> Conversation { get; init; }
}
