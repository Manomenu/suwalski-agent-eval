using System.Text.Json;
using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

/// <summary>What suwageval sends the agent under test: the conversation so far, whose last item the agent answers.</summary>
public sealed record TurnRequest
{
    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    /// <summary>The scenario's fixtures, verbatim; omitted when it has none.</summary>
    public JsonElement? Fixtures { get; init; }
}
