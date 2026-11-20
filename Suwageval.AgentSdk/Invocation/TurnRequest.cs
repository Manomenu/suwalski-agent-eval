using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

public sealed record TurnRequest
{
    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    public JsonElement? Fixtures { get; init; }

    [JsonIgnore]
    public TurnCorrelation Correlation { get; init; } = TurnCorrelation.None;

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

    public IReadOnlyList<HistoryTurn> HistoryTurns() => HistoryTurnSplitter.Split(Conversation.Take(Conversation.Count - 1));

    public string? ConversationId { get; init; }
}
