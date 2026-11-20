using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Evaluation.Scenarios;

internal static class ScenarioYaml
{
    private static readonly JsonSerializerOptions Shape = new(ContractJson.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Of(ScenarioInput scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var source =
            JsonSerializer.SerializeToNode(scenario, Shape) as JsonObject
            ?? throw new InvalidOperationException("A scenario serializes to a JSON object.");
        var file = new JsonObject { ["key"] = scenario.Key };
        if (scenario.Tags.Count > 0)
        {
            file["tags"] = Required(source, "tags").DeepClone();
        }
        file["conversation"] = new JsonArray([.. ((JsonArray)Required(source, "conversation")).Select(Item)]);
        if (scenario.Continuation != new Continuation())
        {
            file["continuation"] = Required(source, "continuation").DeepClone();
        }
        if (source["fixtures"] is { } fixtures)
        {
            file["fixtures"] = fixtures.DeepClone();
        }
        if (source["origin"] is { } origin)
        {
            file["origin"] = origin.DeepClone();
        }
        return ContractYaml.Of(file);
    }

    private static JsonNode Required(JsonObject node, string name) =>
        node[name] ?? throw new InvalidOperationException($"The serializer writes the '{name}' property here, so it exists.");

    private static JsonNode Item(JsonNode? node)
    {
        var item = node as JsonObject ?? throw new InvalidOperationException("The serializer writes every conversation item as an object.");
        var kind = Required(item, "kind").GetValue<string>();
        var fields = new JsonObject(item.Where(field => field.Key != "kind").Select(field => Copy(field)));
        return kind switch
        {
            "UserMessage" when fields.Count == 1 => new JsonObject { ["user"] = Required(fields, "text").DeepClone() },
            "AssistantMessage" when fields.Count == 1 => new JsonObject { ["assistant"] = Required(fields, "text").DeepClone() },
            "ToolCall" => new JsonObject { ["tool_call"] = fields },
            "ToolResult" => new JsonObject
            {
                ["tool_result"] = new JsonObject(fields.Select(field => Copy(field, field.Key == "callId" ? "call_id" : field.Key))),
            },
            "Event" => new JsonObject { ["event"] = fields },
            _ => item.DeepClone(),
        };
    }

    private static KeyValuePair<string, JsonNode?> Copy(KeyValuePair<string, JsonNode?> field, string? name = null) =>
        new(name ?? field.Key, field.Value?.DeepClone());
}
