namespace Suwageval.Server.Evaluation.Scenarios;

// Why: properties, not a constructor, because Dapper cannot match a constructor to a text[] column
internal sealed record ScenarioRow
{
    public required Guid Id { get; init; }
    public required Guid AgentId { get; init; }
    public required string Key { get; init; }
    public required string[] Tags { get; init; }
    public required int Revision { get; init; }
    public required string Content { get; init; }
    public required string ContentHash { get; init; }
    public required DateTime CreatedAt { get; init; }
}
