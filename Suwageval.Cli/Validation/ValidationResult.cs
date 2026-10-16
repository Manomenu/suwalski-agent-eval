using Suwageval.Cli.Files;

namespace Suwageval.Cli.Validation;

internal sealed record ValidationResult(IReadOnlyList<Problem> Problems, int Scenarios, int Checks)
{
    public bool IsValid => Problems.Count == 0;
}
