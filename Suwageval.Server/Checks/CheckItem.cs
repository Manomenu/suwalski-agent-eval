namespace Suwageval.Server.Checks;

/// <summary>One check of an upsert: its key, the key of the scenario it belongs to, and its content as JSON (the caller serializes it).</summary>
internal sealed record CheckItem(string Key, string ScenarioKey, string ContentJson);
