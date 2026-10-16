using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Push;

/// <summary>What a push did, as <c>--json</c> prints it: the agent as the server knows it, and each scenario's and check's outcome.</summary>
internal sealed record PushResult(
    Agent Agent,
    bool AgentCreated,
    IReadOnlyList<ScenarioResult> Scenarios,
    IReadOnlyList<CheckResult> Checks
);
