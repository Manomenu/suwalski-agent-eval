namespace Suwageval.Contracts.Scenarios;

/// <summary>What happened to each scenario of the request, in its order.</summary>
public sealed record PutScenariosResponse(IReadOnlyList<ScenarioResult> Results);
