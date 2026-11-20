using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Scenarios;

public sealed record Continuation
{
    public int Turns { get; init; } = 1;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SimulatedUser { get; init; }
}
