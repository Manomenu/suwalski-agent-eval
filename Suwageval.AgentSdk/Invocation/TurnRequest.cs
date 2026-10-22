using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

/// <summary>What suwageval sends the agent under test: the conversation so far, whose last item the agent answers.</summary>
public sealed record TurnRequest
{
    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    /// <summary>The scenario's fixtures, verbatim; omitted when it has none. <see cref="FixturesAs{T}"/> reads them as a type.</summary>
    public JsonElement? Fixtures { get; init; }

    /// <summary>Read from the request's headers by <see cref="SuwagevalAgentEndpoints.MapSuwagevalAgent"/>; never part of the body.</summary>
    [JsonIgnore]
    public TurnCorrelation Correlation { get; init; } = new(null, null, null);

    /// <summary>
    /// The fixtures as <typeparamref name="T"/>, or null when the scenario has none. A member the type does not declare is
    /// rejected, like the schema in the agent's contract would.
    /// </summary>
    /// <exception cref="TurnRejectedException">The fixtures do not fit <typeparamref name="T"/>.</exception>
    public T? FixturesAs<T>()
        where T : class
    {
        if (Fixtures is null or { ValueKind: JsonValueKind.Null })
        {
            return null;
        }
        try
        {
            return Fixtures.Value.Deserialize<T>(ContractJson.Options);
        }
        catch (JsonException error)
        {
            throw new TurnRejectedException($"The fixtures do not match the declared schema: {error.Message}");
        }
    }

    /// <summary>
    /// The conversation before the last item, split into turns: each user message with what followed it, tool results paired
    /// with their calls.
    /// </summary>
    /// <exception cref="TurnRejectedException">
    /// A tool call id appears twice, a tool result has no call before it, or a turn's tool calls are not closed by an
    /// assistant message.
    /// </exception>
    public IReadOnlyList<HistoryTurn> HistoryTurns() => HistoryTurnSplitter.Split(Conversation.Take(Conversation.Count - 1));
}
