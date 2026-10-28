namespace Suwageval.Contracts.Scenarios;

/// <summary>What happened to each scenario of the request, in its order, then to each scenario it deleted.</summary>
public sealed record PutScenariosResponse(IReadOnlyList<ScenarioResult> Results);
