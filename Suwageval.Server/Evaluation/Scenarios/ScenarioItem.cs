namespace Suwageval.Server.Evaluation.Scenarios;

internal sealed record ScenarioItem(string Key, IReadOnlyList<string> Tags, string ContentJson);
