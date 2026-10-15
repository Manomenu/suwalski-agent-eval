using System.Text.Json;

namespace Suwageval.Contracts.Conversations;

public sealed record Event : ConversationItem
{
    /// <summary>One of the types the agent declares in its context contract.</summary>
    public required string Type { get; init; }

    /// <summary>Free JSON: its shape is the agent's, declared by the event's <c>dataSchema</c>.</summary>
    public required JsonElement Data { get; init; }
}
