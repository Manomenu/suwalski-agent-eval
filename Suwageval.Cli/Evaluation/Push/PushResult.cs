using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Evaluation.Push;

/// <summary>
/// What a push did, as <c>--json</c> prints it: the agent as the server knows it, and each scenario's and check's outcome;
/// <c>ScenariosNotSent</c> counts the scenario files left unsent because the agent's evaluation is off; then the monitoring checks.
/// </summary>
internal sealed record PushResult(
    Agent Agent,
    bool AgentCreated,
    IReadOnlyList<ScenarioResult> Scenarios,
    IReadOnlyList<CheckResult> Checks,
    int ScenariosNotSent = 0
)
{
    /// <summary>What happened to each monitoring check (<c>monitoring-checks.yaml</c>), with <c>deleted</c> for one the file no longer has.</summary>
    public IReadOnlyList<CheckResult> MonitoringChecks { get; init; } = [];

    /// <summary>The monitoring checks left unsent because the agent's monitoring is off.</summary>
    public int MonitoringChecksNotSent { get; init; }
}
