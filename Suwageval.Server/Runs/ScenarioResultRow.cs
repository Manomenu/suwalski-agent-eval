using System.Text.Json;
using Suwageval.AgentSdk;

namespace Suwageval.Server.Runs;

/// <summary>A row of <c>scenario_results</c> with the key of its scenario, the content of the revision it ran and the scenario's tags now.</summary>
internal sealed record ScenarioResultRow(
    Guid Id,
    string ScenarioKey,
    int ScenarioRevision,
    int Sample,
    string Status,
    string? Output,
    string? Error,
    string Undeclared,
    int? DurationMs,
    Guid ScenarioId,
    string ScenarioContent,
    string ScenarioTagsJson,
    string? TurnDurationsJson,
    bool EndedByUser
)
{
    /// <summary>How long the agent took for each turn of a simulation; null for a sample of one turn.</summary>
    public IReadOnlyList<int>? TurnDurationsMs =>
        TurnDurationsJson is null ? null : JsonSerializer.Deserialize<IReadOnlyList<int>>(TurnDurationsJson, ContractJson.Options);
}
