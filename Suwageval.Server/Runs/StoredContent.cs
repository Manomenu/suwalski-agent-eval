using System.Text.Json;
using System.Text.Json.Nodes;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;

namespace Suwageval.Server.Runs;

/// <summary>
/// Reads the content the slices stored (the JSON the caller serialized) back into contract records. Lenient about fields
/// the run does not need — a scenario's key and tags are stored beside the content too.
/// </summary>
internal static class StoredContent
{
    public static ScenarioContent Scenario(string json) =>
        JsonSerializer.Deserialize<ScenarioContent>(json, ContractContent.Lenient)
        ?? throw new InvalidOperationException("A stored scenario is empty.");

    /// <summary>The check's key and scenario are columns of the row; the stored content may or may not repeat them.</summary>
    public static CheckInput Check(CheckRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var content = JsonNode.Parse(row.Content) as JsonObject ?? throw new InvalidOperationException("A stored check is not an object.");
        content["key"] ??= row.Key;
        content["scenarioKey"] ??= row.ScenarioKey;
        return content.Deserialize<CheckInput>(ContractContent.Lenient) ?? throw new InvalidOperationException("A stored check is empty.");
    }
}
