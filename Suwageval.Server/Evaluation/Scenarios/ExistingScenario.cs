namespace Suwageval.Server.Evaluation.Scenarios;

internal sealed record ExistingScenario
{
    public required Guid Id { get; init; }
    public required bool Deleted { get; init; }
    public required string[] Tags { get; init; }
    public required int Revision { get; init; }
    public required string ContentHash { get; init; }
}
