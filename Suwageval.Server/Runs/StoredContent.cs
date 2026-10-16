using System.Text.Json;
using System.Text.Json.Nodes;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Checks;

namespace Suwageval.Server.Runs;

/// <summary>
/// Reads the content the slices stored (the JSON the caller serialized) back into contract records. Lenient about fields
/// the run does not need — a scenario's key and tags are stored beside the content too.
/// </summary>
internal static class StoredContent
{
    private static readonly JsonSerializerOptions Lenient = new(ContractJson.Options)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    public static ScenarioContent Scenario(string json) =>
        JsonSerializer.Deserialize<ScenarioContent>(json, Lenient) ?? throw new InvalidOperationException("A stored scenario is empty.");

    public static AgentVersionInput Version(string json) =>
        JsonSerializer.Deserialize<AgentVersionInput>(json, Lenient) ?? throw new InvalidOperationException("A stored version is empty.");

    /// <summary>The check's key and scenario are columns of the row; the stored content may or may not repeat them.</summary>
    public static CheckInput Check(CheckRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var content = JsonNode.Parse(row.Content) as JsonObject ?? throw new InvalidOperationException("A stored check is not an object.");
        content["key"] ??= row.Key;
        content["scenarioKey"] ??= row.ScenarioKey;
        return content.Deserialize<CheckInput>(Lenient) ?? throw new InvalidOperationException("A stored check is empty.");
    }
}
