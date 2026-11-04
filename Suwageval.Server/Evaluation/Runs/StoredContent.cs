using System.Text.Json;
using System.Text.Json.Nodes;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;

namespace Suwageval.Server.Evaluation.Runs;

internal static class StoredContent
{
    public static ScenarioContent Scenario(string json) =>
        JsonSerializer.Deserialize<ScenarioContent>(json, ContractContent.Lenient)
        ?? throw new InvalidOperationException("A stored scenario is empty.");

    public static CheckInput Check(CheckRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var content = JsonNode.Parse(row.Content) as JsonObject ?? throw new InvalidOperationException("A stored check is not an object.");
        content["key"] ??= row.Key;
        content["scenarioKey"] ??= row.ScenarioKey;
        return content.Deserialize<CheckInput>(ContractContent.Lenient) ?? throw new InvalidOperationException("A stored check is empty.");
    }
}
