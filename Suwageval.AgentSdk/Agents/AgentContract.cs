namespace Suwageval.AgentSdk.Agents;

public sealed record AgentContract
{
    public string? Label { get; init; }

    public required IReadOnlyList<LlmAgentDefinition> Agents { get; init; }

    public string? OtherConfig { get; init; }

    public ContextDefinition Context { get; init; } = new();
}
