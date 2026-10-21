namespace Suwageval.Contracts.Runs;

/// <summary>A group of an agent's runs (run --group) at a glance: where the variants page of the group starts.</summary>
public sealed record RunGroupSummary
{
    public required string Name { get; init; }

    public required int Runs { get; init; }

    /// <summary>How many different agent versions the runs used: the variants, when each run is of its own version.</summary>
    public required int Versions { get; init; }

    public required DateTime NewestRunAt { get; init; }

    /// <summary>When the group was archived (hidden by default, restorable); null for a group in use.</summary>
    public DateTime? ArchivedAt { get; init; }
}
