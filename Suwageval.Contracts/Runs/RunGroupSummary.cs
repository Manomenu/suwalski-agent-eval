namespace Suwageval.Contracts.Runs;

public sealed record RunGroupSummary
{
    public required string Name { get; init; }

    public required int Runs { get; init; }

    public required int Versions { get; init; }

    public required DateTime NewestRunAt { get; init; }

    public DateTime? ArchivedAt { get; init; }
}
