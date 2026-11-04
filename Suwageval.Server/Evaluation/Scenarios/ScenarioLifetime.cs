namespace Suwageval.Server.Evaluation.Scenarios;

// Why: properties, not a constructor, because Dapper cannot match a constructor to a text[] column
internal sealed record ScenarioLifetime
{
    public required string Key { get; init; }
    public required string[] Tags { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}
