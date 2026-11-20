using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

public sealed record EventDeclaration
{
    public required string Type { get; init; }

    public required JsonElement DataSchema { get; init; }
}
