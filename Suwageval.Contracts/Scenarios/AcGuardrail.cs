using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Scenarios;

public sealed record AcGuardrail
{
    public required string Id { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryLimit { get; init; }
}
