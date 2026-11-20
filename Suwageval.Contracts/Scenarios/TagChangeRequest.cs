namespace Suwageval.Contracts.Scenarios;

public sealed record TagChangeRequest(IReadOnlyList<string> ScenarioKeys, IReadOnlyList<string> Add, IReadOnlyList<string> Remove);
