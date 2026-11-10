using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>
/// Reads a text check's <c>contains</c> or <c>notContains</c> as one text or a list of texts, and writes one text as a plain string,
/// so a check written before lists existed keeps its stored form and its content hash.
/// </summary>
public sealed class TextsConverter : JsonConverter<IReadOnlyList<string>>
{
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return [reader.GetString() ?? ""];
        }
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException(Shape);
        }
        var texts = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            texts.Add(reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : throw new JsonException(Shape));
        }
        return texts;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Count == 1)
        {
            writer.WriteStringValue(value[0]);
            return;
        }
        writer.WriteStartArray();
        foreach (var text in value)
        {
            writer.WriteStringValue(text);
        }
        writer.WriteEndArray();
    }

    private const string Shape = "contains and notContains are a text or a list of texts.";
}
