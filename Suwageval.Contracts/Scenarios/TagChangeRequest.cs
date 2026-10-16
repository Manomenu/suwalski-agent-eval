namespace Suwageval.Contracts.Scenarios;

/// <summary>Adds and removes tags on several scenarios at once; tags are metadata and make no new revision.</summary>
public sealed record TagChangeRequest(IReadOnlyList<string> ScenarioKeys, IReadOnlyList<string> Add, IReadOnlyList<string> Remove);
