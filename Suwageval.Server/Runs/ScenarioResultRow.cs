namespace Suwageval.Server.Runs;

/// <summary>A row of <c>scenario_results</c> with the key of its scenario.</summary>
internal sealed record ScenarioResultRow(
    Guid Id,
    string ScenarioKey,
    int ScenarioRevision,
    int Sample,
    string Status,
    string? Output,
    string? Error
);
