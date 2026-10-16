namespace Suwageval.Contracts.Runs;

/// <summary>One earlier run of a check's history and whether the check passed in it.</summary>
public sealed record CheckHistoryPoint
{
    public required Guid RunId { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required bool Passed { get; init; }
}
