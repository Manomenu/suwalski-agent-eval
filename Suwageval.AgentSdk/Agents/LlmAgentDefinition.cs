namespace Suwageval.AgentSdk.Agents;

public sealed record LlmAgentDefinition
{
    public required string Name { get; init; }

    public required ModelDefinition Model { get; init; }

    public required string Config { get; init; }

    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];
}
