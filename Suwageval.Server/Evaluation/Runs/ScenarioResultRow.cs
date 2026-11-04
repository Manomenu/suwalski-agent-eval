using System.Text.Json;
using Suwageval.AgentSdk;

namespace Suwageval.Server.Evaluation.Runs;

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
    public IReadOnlyList<int>? TurnDurationsMs =>
        TurnDurationsJson is null ? null : JsonSerializer.Deserialize<IReadOnlyList<int>>(TurnDurationsJson, ContractJson.Options);
}
