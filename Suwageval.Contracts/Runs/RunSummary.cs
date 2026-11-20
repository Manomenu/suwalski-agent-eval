namespace Suwageval.Contracts.Runs;

public sealed record RunSummary
{
    public required Guid Id { get; init; }

    public required string Agent { get; init; }

    public required Guid AgentVersionId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Label { get; init; }

    public string? DataVersion { get; init; }

    public string? DataDescription { get; init; }

    public required string EndpointUrl { get; init; }

    public string? Group { get; init; }

    public string? Note { get; init; }

    public required RunStatus Status { get; init; }

    public string? Error { get; init; }

    public required int SamplesTotal { get; init; }

    public required int SamplesDone { get; init; }

    public int SamplesFailed { get; init; }

    public required int ChecksPassed { get; init; }

    public required int ChecksFailed { get; init; }

    public bool ContractChangedDuringRun { get; init; }

    public bool CancelRequested { get; init; }

    public required DateTime CreatedAt { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    public Guid? RecheckOf { get; init; }

    public IReadOnlyList<SkippedScenario> SkippedScenarios { get; init; } = [];

    public RunOrigin? Origin { get; init; }

    public bool Reference { get; init; }

    public string? ComparedWith { get; init; }

    public IReadOnlyList<string> JudgeModels { get; init; } = [];
}
