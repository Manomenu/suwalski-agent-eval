namespace Suwageval.Server.Scenarios;

/// <summary>The tags of one scenario after a change.</summary>
/// <remarks>Properties, not a constructor: Dapper cannot match a constructor to a <c>text[]</c> column.</remarks>
internal sealed record ScenarioTags
{
    public required string Key { get; init; }
    public required string[] Tags { get; init; }
}
