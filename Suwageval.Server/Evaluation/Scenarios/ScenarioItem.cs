namespace Suwageval.Server.Evaluation.Scenarios;

/// <summary>One scenario of an upsert: its key, its tags and its content as JSON (the caller serializes it).</summary>
internal sealed record ScenarioItem(string Key, IReadOnlyList<string> Tags, string ContentJson);
