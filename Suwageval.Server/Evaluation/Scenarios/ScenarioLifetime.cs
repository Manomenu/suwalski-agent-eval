namespace Suwageval.Server.Evaluation.Scenarios;

internal sealed record ScenarioLifetime
{
    public required string Key { get; init; }
    public required string[] Tags { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}
