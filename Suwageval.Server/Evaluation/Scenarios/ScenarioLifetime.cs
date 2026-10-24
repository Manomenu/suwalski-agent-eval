namespace Suwageval.Server.Evaluation.Scenarios;

/// <summary>A scenario of an agent with the span it existed: from its first revision until it was deleted (null: still live).</summary>
/// <remarks>Properties, not a constructor: Dapper cannot match a constructor to a <c>text[]</c> column.</remarks>
internal sealed record ScenarioLifetime
{
    public required string Key { get; init; }
    public required string[] Tags { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}
