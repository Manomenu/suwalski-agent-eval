namespace Suwageval.AgentSdk.Agents;

/// <summary>What the system under test was configured as when a run happened: its LLM agents, and what belongs to the system as a whole.</summary>
public sealed record AgentContract
{
    public string? Label { get; init; }

    /// <summary>The LLM agents the system is made of, at least one; each has its own model, config and tools.</summary>
    public required IReadOnlyList<LlmAgentDefinition> Agents { get; init; }

    /// <summary>Free text for what belongs to the system, not to one agent.</summary>
    public string? OtherConfig { get; init; }

    public ContextDefinition Context { get; init; } = new();
}
