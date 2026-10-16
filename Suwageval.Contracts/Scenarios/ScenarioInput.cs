using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.Contracts.Conversations;

namespace Suwageval.Contracts.Scenarios;

/// <summary>A frozen moment of a conversation; the agent answers its last message.</summary>
public sealed record ScenarioInput
{
    /// <summary>Lowercase kebab-case, unique within the agent.</summary>
    public required string Key { get; init; }

    /// <summary>Lowercase kebab-case; a run picks scenarios by tag.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    /// <summary>
    /// Test data the agent is started with, sent verbatim with every turn. Free JSON, shaped by the agent's
    /// <c>context.fixturesSchema</c>; a scenario that has it needs an agent that declares the schema.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Fixtures { get; init; }

    public Continuation Continuation { get; init; } = new();

    /// <summary>
    /// How many times the conversation is repeated, for agents that do not answer the same way twice (1 to 20); a check can set its own.
    /// Not set: the agent's <c>samples</c>, else 3.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Samples { get; init; }
}
