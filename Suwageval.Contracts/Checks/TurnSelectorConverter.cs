using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>Writes a <see cref="TurnSelector"/> as a number or as "last" / "any".</summary>
public sealed class TurnSelectorConverter : JsonConverter<TurnSelector>
{
    public override TurnSelector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number) && number >= 1)
        {
            return new TurnSelector(TurnSelectorKind.Number, number);
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            switch (reader.GetString())
            {
                case "any":
                    return new TurnSelector(TurnSelectorKind.Any);
                case "last":
                    return new TurnSelector(TurnSelectorKind.Last);
                default:
                    break;
            }
        }
        throw new JsonException("turn must be a number from 1, \"last\" or \"any\".");
    }

    public override void Write(Utf8JsonWriter writer, TurnSelector value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        switch (value.Kind)
        {
            case TurnSelectorKind.Number:
                writer.WriteNumberValue(value.Number);
                break;
            case TurnSelectorKind.Last:
                writer.WriteStringValue("last");
                break;
            case TurnSelectorKind.Any:
            default:
                writer.WriteStringValue("any");
                break;
        }
    }
}
