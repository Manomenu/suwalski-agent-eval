using System.Text.Json;
using System.Text.Json.Nodes;

namespace Suwageval.AgentSdk.Agents;

public static class ContractSchema
{
    public static JsonObject EnumSchema<TEnum>()
        where TEnum : struct, Enum =>
        new() { ["type"] = "string", ["enum"] = new JsonArray([.. Enum.GetNames<TEnum>().Select(name => (JsonNode)name)]) };

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
