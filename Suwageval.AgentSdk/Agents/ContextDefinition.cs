using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

public sealed record ContextDefinition
{
    public bool UsesRetrieval { get; init; }

    public RetrievalDefinition? Retrieval { get; init; }

    public IReadOnlyList<EventDeclaration> Events { get; init; } = [];

    public JsonElement? FixturesSchema { get; init; }

    [OmittedWhenEmpty]
    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];
}
