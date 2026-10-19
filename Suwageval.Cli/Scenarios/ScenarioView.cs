using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Scenarios;

/// <summary>What <c>suwageval scenario</c> shows of one scenario, and what its <c>--json</c> prints.</summary>
internal sealed record ScenarioView(ScenarioDetail Scenario, IReadOnlyList<CheckDetail> Checks, IReadOnlyList<ScenarioRevision> Revisions);
