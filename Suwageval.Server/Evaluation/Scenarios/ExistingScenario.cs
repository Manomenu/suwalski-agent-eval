namespace Suwageval.Server.Evaluation.Scenarios;

/// <summary>What an upsert needs to know about a scenario already stored: its latest revision and whether it is deleted.</summary>
/// <remarks>Properties, not a constructor: Dapper cannot match a constructor to a <c>text[]</c> column.</remarks>
internal sealed record ExistingScenario
{
    public required Guid Id { get; init; }
    public required bool Deleted { get; init; }
    public required string[] Tags { get; init; }
    public required int Revision { get; init; }
    public required string ContentHash { get; init; }
}
