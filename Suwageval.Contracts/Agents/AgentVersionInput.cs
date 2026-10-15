namespace Suwageval.Contracts.Agents;

/// <summary>What the agent was configured as when a run happened.</summary>
public sealed record AgentVersionInput
{
    public string? Label { get; init; }

    public required string SystemPrompt { get; init; }

    public required ModelDefinition Model { get; init; }

    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];

    public ContextDefinition Context { get; init; } = new();
}
