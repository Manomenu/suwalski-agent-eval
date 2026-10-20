using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

public sealed record EventDeclaration
{
    public required string Type { get; init; }

    /// <summary>The shape of the event's <c>data</c>, as a JSON Schema document.</summary>
    public required JsonElement DataSchema { get; init; }
}
