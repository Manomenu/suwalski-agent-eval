namespace Suwageval.Server.Evaluation.Scenarios;

internal sealed record ScenarioTags
{
    public required string Key { get; init; }
    public required string[] Tags { get; init; }
}
