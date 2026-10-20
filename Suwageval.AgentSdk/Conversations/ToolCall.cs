using System.Text.Json;

namespace Suwageval.AgentSdk.Conversations;

public sealed record ToolCall : ConversationItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Free JSON: its shape is the agent's, declared by the tool's parameters in the agent's contract.</summary>
    public required JsonElement Arguments { get; init; }

    /// <summary>The name of the LLM agent (from the contract's agents) that made the call.</summary>
    public string? Agent { get; init; }
}
