using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Scenarios;

/// <summary>
/// A scenario written as a scenario file (docs/scenario-format.md): the fields in the documented order, the conversation
/// in the log shorthands (<c>- user:</c>, <c>- assistant:</c>, <c>- tool_call:</c>, <c>- tool_result:</c>, <c>- event:</c>), the full
/// form with <c>kind:</c> only where a shorthand cannot say the item.
/// </summary>
internal static class ScenarioYaml
{
    private static readonly JsonSerializerOptions Shape = new(ContractJson.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Of(ScenarioInput scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var source = (JsonObject)JsonSerializer.SerializeToNode(scenario, Shape)!;
        var file = new JsonObject { ["key"] = scenario.Key };
        if (scenario.Tags.Count > 0)
        {
            file["tags"] = source["tags"]!.DeepClone();
        }
        file["conversation"] = new JsonArray([.. ((JsonArray)source["conversation"]!).Select(item => Item((JsonObject)item!))]);
        if (scenario.Continuation.Turns != new Continuation().Turns)
        {
            file["continuation"] = source["continuation"]!.DeepClone();
        }
        if (source["fixtures"] is { } fixtures)
        {
            file["fixtures"] = fixtures.DeepClone();
        }
        return ContractYaml.Of(file);
    }

    private static JsonNode Item(JsonObject item)
    {
        var kind = item["kind"]!.GetValue<string>();
        var fields = new JsonObject(item.Where(field => field.Key != "kind").Select(field => Copy(field)));
        return kind switch
        {
            "UserMessage" when fields.Count == 1 => new JsonObject { ["user"] = fields["text"]!.DeepClone() },
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
