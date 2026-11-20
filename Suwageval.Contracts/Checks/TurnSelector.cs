using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

[JsonConverter(typeof(TurnSelectorConverter))]
public readonly record struct TurnSelector(TurnSelectorKind Kind, int Number = 0);
