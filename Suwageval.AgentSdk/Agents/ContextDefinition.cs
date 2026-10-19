using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

/// <summary>What besides messages and tool calls may appear in the agent's conversation log.</summary>
public sealed record ContextDefinition
{
    public bool UsesRetrieval { get; init; }

    /// <summary>The version of the data retrieval reads: required with <c>UsesRetrieval</c>, absent without it.</summary>
    public RetrievalDefinition? Retrieval { get; init; }

    public IReadOnlyList<EventDeclaration> Events { get; init; } = [];

    /// <summary>A JSON Schema of the scenario fixtures: present means the agent accepts them.</summary>
    public JsonElement? FixturesSchema { get; init; }
}
