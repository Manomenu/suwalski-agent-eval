using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

public sealed record ToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required JsonElement Parameters { get; init; }
}
