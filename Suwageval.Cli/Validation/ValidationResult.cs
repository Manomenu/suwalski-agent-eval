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
    public bool IsValid => Problems.Count == 0;
}
