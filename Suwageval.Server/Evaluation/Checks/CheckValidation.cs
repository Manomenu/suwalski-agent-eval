using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Evaluation.Checks;

/// <summary>
/// What a request about checks must satisfy, checked where it enters: the rules every check obeys (<see cref="CheckRules"/>, the
/// same ones <c>suwageval validate</c> applies) and what only a request can break — keys used twice, the scenario key.
/// </summary>
internal static class CheckValidation
{
    public static List<FieldError> Validate(IReadOnlyList<CheckInput> checks)
    {
        var errors = new List<FieldError>();
        var keys = new HashSet<string>();
        for (var i = 0; i < checks.Count; i++)
        {
            var check = checks[i];
            var path = $"checks[{i}]";
            errors.AddRange(CheckRules.Problems(check, path).Select(problem => new FieldError(problem.Path, problem.Message)));
            if (Kebab.IsValid(check.Key) && !keys.Add(check.Key))
            {
                errors.Add(new FieldError($"{path}.key", $"The check key '{check.Key}' is used twice in the request."));
            }
            if (!Kebab.IsValid(check.ScenarioKey))
            {
                errors.Add(new FieldError($"{path}.scenarioKey", $"The scenario key '{check.ScenarioKey}' is not lowercase kebab-case."));
            }
        }
        return errors;
    }
}
