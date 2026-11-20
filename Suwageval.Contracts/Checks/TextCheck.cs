using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

public sealed record TextCheck : CheckInput
{
    [JsonConverter(typeof(TextsConverter))]
    public IReadOnlyList<string>? Contains { get; init; }

    [JsonConverter(typeof(TextsConverter))]
    public IReadOnlyList<string>? NotContains { get; init; }

    public string? Pattern { get; init; }
}
