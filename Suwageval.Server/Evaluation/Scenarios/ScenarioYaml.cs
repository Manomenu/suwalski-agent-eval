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
        // Why: the serializer writes every scenario property, so the node and its fields exist
        var source = (JsonObject)JsonSerializer.SerializeToNode(scenario, Shape)!;
        var file = new JsonObject { ["key"] = scenario.Key };
        if (scenario.Tags.Count > 0)
        {
            // Why: the serializer writes the tags array for every scenario
            file["tags"] = source["tags"]!.DeepClone();
        }
        // Why: the serializer writes the conversation array and every item is an object
        file["conversation"] = new JsonArray([.. ((JsonArray)source["conversation"]!).Select(item => Item((JsonObject)item!))]);
        if (scenario.Continuation != new Continuation())
        {
            // Why: the serializer writes the continuation property
            file["continuation"] = source["continuation"]!.DeepClone();
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

    private static JsonNode Item(JsonObject item)
    {
        // Why: every serialized conversation item has a kind
        var kind = item["kind"]!.GetValue<string>();
        var fields = new JsonObject(item.Where(field => field.Key != "kind").Select(field => Copy(field)));
        return kind switch
        {
            // Why: a user message always has a text field
            "UserMessage" when fields.Count == 1 => new JsonObject { ["user"] = fields["text"]!.DeepClone() },
            // Why: an assistant message always has a text field
            "AssistantMessage" when fields.Count == 1 => new JsonObject { ["assistant"] = fields["text"]!.DeepClone() },
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
