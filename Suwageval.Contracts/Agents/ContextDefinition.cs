namespace Suwageval.Contracts.Agents;

/// <summary>What besides messages and tool calls may appear in the agent's conversation log.</summary>
public sealed record ContextDefinition
{
    public bool UsesRetrieval { get; init; }

    public IReadOnlyList<EventDeclaration> Events { get; init; } = [];
}
