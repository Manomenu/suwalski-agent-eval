namespace Suwageval.Contracts.Runs;

public sealed record AgentError
{
    public required string ScenarioKey { get; init; }

    public required Guid AgentVersionId { get; init; }

    public string? VersionLabel { get; init; }

    public required int Errors { get; init; }

    public required int Samples { get; init; }

    public required Guid NewestErrorRunId { get; init; }

    public required string NewestError { get; init; }
}
