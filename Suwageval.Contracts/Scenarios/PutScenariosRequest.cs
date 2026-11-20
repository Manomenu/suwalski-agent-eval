namespace Suwageval.Contracts.Scenarios;

public sealed record PutScenariosRequest(IReadOnlyList<ScenarioInput> Scenarios);
