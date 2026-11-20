using System.Text.Json;

namespace Suwageval.AgentSdk.Conversations;

public sealed record ToolCall : ConversationItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required JsonElement Arguments { get; init; }

    public string? Agent { get; init; }
}
