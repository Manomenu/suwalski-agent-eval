using System.Text.Json;
using System.Text.Json.Nodes;

namespace Suwageval.AgentSdk.Agents;

/// <summary>Builders for the JSON schemas an <see cref="AgentContract"/> declares (an event's data, the fixtures).</summary>
public static class ContractSchema
{
    /// <summary>A string that is one of the names of <typeparamref name="TEnum"/>.</summary>
    public static JsonObject EnumSchema<TEnum>()
        where TEnum : struct, Enum =>
        new() { ["type"] = "string", ["enum"] = new JsonArray([.. Enum.GetNames<TEnum>().Select(name => (JsonNode)name)]) };

    /// <summary>An object with exactly these <paramref name="properties"/>, of which <paramref name="required"/> must be present.</summary>
    public static JsonElement ObjectSchema(JsonObject properties, params string[] required)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return JsonSerializer.SerializeToElement(
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = new JsonArray([.. required.Select(name => (JsonNode)name)]),
                ["additionalProperties"] = false,
            }
        );
    }
}
