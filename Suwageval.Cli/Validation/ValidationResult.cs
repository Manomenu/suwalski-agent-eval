using Suwageval.Cli.Files;

namespace Suwageval.Cli.Validation;

internal sealed record ValidationResult(
    IReadOnlyList<Problem> Problems,
    int Scenarios,
    int Checks,
    bool NamesChecked = false,
    string? Notice = null
)
{
    public LoadedAgent? Agent { get; init; }

    public IReadOnlyList<LoadedScenario> LoadedScenarios { get; init; } = [];

    public IReadOnlyList<Contracts.Checks.CheckInput>? MonitoringChecks { get; init; }

    public bool IsValid => Problems.Count == 0;
}
