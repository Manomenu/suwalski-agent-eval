using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Files;

internal sealed record LoadedScenario(YamlSource Source, ScenarioInput Scenario, IReadOnlyList<CheckInput> Checks);
