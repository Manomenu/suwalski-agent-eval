namespace Suwageval.Contracts.Scenarios;

/// <summary>The agent's whole set of scenarios; a stored scenario not listed is deleted (its revisions and past runs stay).</summary>
public sealed record PutScenariosRequest(IReadOnlyList<ScenarioInput> Scenarios);
