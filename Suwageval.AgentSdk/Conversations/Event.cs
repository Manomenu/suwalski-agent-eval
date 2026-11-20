using System.Text.Json;

namespace Suwageval.AgentSdk.Conversations;

public sealed record Event : ConversationItem
{
    public required string Type { get; init; }

    public required JsonElement Data { get; init; }
}
