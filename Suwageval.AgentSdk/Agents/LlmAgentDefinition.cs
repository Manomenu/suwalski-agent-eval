namespace Suwageval.AgentSdk.Agents;

/// <summary>One LLM agent of the system under test.</summary>
public sealed record LlmAgentDefinition
{
    public required string Name { get; init; }

    public required ModelDefinition Model { get; init; }

    /// <summary>Free text: the system prompt, a scope policy, whatever defines this agent. May be empty.</summary>
    public required string Config { get; init; }

    /// <summary>The tools this agent may call.</summary>
    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];
}
