using System.Text.Json;
using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

/// <summary>What suwageval sends the agent under test: the conversation so far, whose last item the agent answers.</summary>
public sealed record TurnRequest
{
    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    /// <summary>The scenario's fixtures, verbatim; omitted when it has none.</summary>
    public JsonElement? Fixtures { get; init; }

    /// <summary>
    /// The same for every turn of one simulated conversation (a scenario with a simulated user), omitted otherwise. An agent that keeps
    /// state — its test data, a state machine — keeps it under this id: the first turn of an id starts from fresh test data, every
    /// later turn of it goes on from what the earlier ones left. The conversation sent is still the whole log.
    /// </summary>
    public string? ConversationId { get; init; }
}
