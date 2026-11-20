namespace Suwageval.Contracts.Runs;

public sealed record UnstableCheck
{
    public required string ScenarioKey { get; init; }

    public required string CheckKey { get; init; }

    public required int CheckRevision { get; init; }

    public required Guid AgentVersionId { get; init; }

    public string? VersionLabel { get; init; }

    public required int Runs { get; init; }

    public required int PassedRuns { get; init; }

    public required Guid NewestFailedRunId { get; init; }
}
