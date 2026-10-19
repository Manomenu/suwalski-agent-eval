using System.Text.Json;

namespace Suwageval.Contracts.Agents;

public sealed record ToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>The arguments the tool takes, as a JSON Schema document.</summary>
    public required JsonElement Parameters { get; init; }
}
