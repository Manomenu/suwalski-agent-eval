using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>One finished sample of a scenario, with the verdict of every check on it, ready to be stored.</summary>
internal sealed record NewSample(
    Guid ScenarioId,
    int ScenarioRevision,
    int Sample,
    SampleStatus Status,
    string? OutputJson,
    string? Error,
    int? DurationMs,
    IReadOnlyList<string> Undeclared,
    IReadOnlyList<NewCheckResult> Checks
);
