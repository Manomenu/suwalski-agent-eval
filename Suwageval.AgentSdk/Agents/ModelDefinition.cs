using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

public sealed record ModelDefinition
{
    public required string Name { get; init; }

    public JsonElement? Parameters { get; init; }
}
