using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Evaluation.Push;

internal sealed record PushResult(
    Agent Agent,
    bool AgentCreated,
    IReadOnlyList<ScenarioResult> Scenarios,
    IReadOnlyList<CheckResult> Checks,
    int ScenariosNotSent = 0
)
{
    public IReadOnlyList<CheckResult> MonitoringChecks { get; init; } = [];

    public int MonitoringChecksNotSent { get; init; }
}
