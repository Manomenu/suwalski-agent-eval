using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>Which turn a check looks at: a number (from 1), <c>"last"</c> or <c>"any"</c>. The default value is "any".</summary>
[JsonConverter(typeof(TurnSelectorConverter))]
public readonly record struct TurnSelector(TurnSelectorKind Kind, int Number = 0);
