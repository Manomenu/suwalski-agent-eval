namespace Suwageval.Contracts.Runs;

public sealed record CheckHistoryPoint
{
    public required Guid RunId { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required bool Passed { get; init; }

    public string? DataVersion { get; init; }
}
