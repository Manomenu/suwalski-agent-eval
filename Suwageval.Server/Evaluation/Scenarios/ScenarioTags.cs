namespace Suwageval.Server.Evaluation.Scenarios;

// Why: properties, not a constructor, because Dapper cannot match a constructor to a text[] column
internal sealed record ScenarioTags
{
    public required string Key { get; init; }
    public required string[] Tags { get; init; }
}
