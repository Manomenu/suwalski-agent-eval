namespace Suwageval.Contracts.Scenarios;

/// <summary>The scenarios to write for an agent; keys not listed are left alone.</summary>
public sealed record PutScenariosRequest(IReadOnlyList<ScenarioInput> Scenarios);
