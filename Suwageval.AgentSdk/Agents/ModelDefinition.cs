using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

public sealed record ModelDefinition
{
    public required string Name { get; init; }

    /// <summary>Free JSON (temperature and the like): which parameters exist is the model provider's business.</summary>
    public JsonElement? Parameters { get; init; }
}
