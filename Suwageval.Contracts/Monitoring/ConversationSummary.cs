namespace Suwageval.Contracts.Monitoring;

public sealed record ConversationSummary
{
    public required Guid Id { get; init; }

    public required string Agent { get; init; }

    public required string Environment { get; init; }

    public required string ConversationId { get; init; }

    public required Guid AgentVersionId { get; init; }

    public string? VersionLabel { get; init; }

    public bool VersionChanged { get; init; }

    public string? DataVersion { get; init; }

    public required ConversationState State { get; init; }

    public required int Turns { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset LastTurnAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public DateTimeOffset? CheckedAt { get; init; }

    public int FailedChecks { get; init; }

    public int NotJudgedChecks { get; init; }
}
