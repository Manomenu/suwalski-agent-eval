using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Evaluation.Scenarios;

internal sealed record ScenarioView(ScenarioDetail Scenario, IReadOnlyList<CheckDetail> Checks, IReadOnlyList<ScenarioRevision> Revisions);
