using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>On the agent's answer text; at least one of the three is given, all given must hold.</summary>
public sealed record TextCheck : CheckInput
{
    /// <summary>A text, or a list of texts, each of which the answer contains.</summary>
    [JsonConverter(typeof(TextsConverter))]
    public IReadOnlyList<string>? Contains { get; init; }

    /// <summary>A text, or a list of texts, none of which the answer contains.</summary>
    [JsonConverter(typeof(TextsConverter))]
    public IReadOnlyList<string>? NotContains { get; init; }

    /// <summary>A regular expression the text must match.</summary>
    public string? Pattern { get; init; }
}
