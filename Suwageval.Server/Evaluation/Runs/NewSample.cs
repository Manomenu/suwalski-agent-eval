using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs;

internal sealed record NewSample(
    Guid ScenarioId,
    int ScenarioRevision,
    int Sample,
    SampleStatus Status,
    string? OutputJson,
    string? Error,
    int? DurationMs,
    IReadOnlyList<string> Undeclared,
    IReadOnlyList<NewCheckResult> Checks,
    IReadOnlyList<int>? TurnDurationsMs = null,
    bool EndedByUser = false
);
