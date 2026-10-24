using Suwageval.Cli.Files;

namespace Suwageval.Cli.Validation;

/// <param name="Problems">What is wrong with the files.</param>
/// <param name="Scenarios">How many scenario files were read.</param>
/// <param name="Checks">How many checks they hold.</param>
/// <param name="NamesChecked">Whether tool and event names were checked against the running agent's contract.</param>
/// <param name="Notice">Why they were not, when that is worth saying (the agent could not be reached).</param>
internal sealed record ValidationResult(
    IReadOnlyList<Problem> Problems,
    int Scenarios,
    int Checks,
    bool NamesChecked = false,
    string? Notice = null
)
{
    /// <summary>The agent file as read, for a caller that goes on with it (push); null when it could not be read.</summary>
    public LoadedAgent? Agent { get; init; }

    /// <summary>The scenario files as read, each with its checks.</summary>
    public IReadOnlyList<LoadedScenario> LoadedScenarios { get; init; } = [];

    /// <summary>The checks of <c>monitoring-checks.yaml</c> as read; null when the folder has no such file.</summary>
    public IReadOnlyList<Contracts.Checks.CheckInput>? MonitoringChecks { get; init; }

    public bool IsValid => Problems.Count == 0;
}
