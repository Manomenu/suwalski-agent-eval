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

    public Continuation Continuation { get; init; } = new();

    /// <summary>How many times the conversation is repeated, for agents that do not answer the same way twice.</summary>
    public int Samples { get; init; } = 1;
}
