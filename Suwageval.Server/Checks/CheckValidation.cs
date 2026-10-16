using System.Text.RegularExpressions;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Checks;

/// <summary>What a request about checks must satisfy, checked where it enters. The rules <c>suwageval validate</c> applies to the same fields.</summary>
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
            if (!Kebab.IsValid(check.Key))
            {
                errors.Add(
                    new FieldError($"{path}.key", $"The check key '{check.Key}' is not lowercase kebab-case (like 'refund-is-called').")
                );
            }
            else if (!keys.Add(check.Key))
            {
                errors.Add(new FieldError($"{path}.key", $"The check key '{check.Key}' is used twice in the request."));
            }
            if (!Kebab.IsValid(check.ScenarioKey))
            {
                errors.Add(new FieldError($"{path}.scenarioKey", $"The scenario key '{check.ScenarioKey}' is not lowercase kebab-case."));
            }
            if (check.PassThreshold is <= 0 or > 1 || double.IsNaN(check.PassThreshold))
            {
                errors.Add(new FieldError($"{path}.passThreshold", "passThreshold is above 0 and at most 1."));
            }
            if (check is TextCheck text)
            {
                AddTextErrors(errors, text, path);
            }
        }
        return errors;
    }

    private static void AddTextErrors(List<FieldError> errors, TextCheck text, string path)
    {
        if (text.Contains is null && text.NotContains is null && text.Pattern is null)
        {
            errors.Add(new FieldError(path, "A text check needs at least one of 'contains', 'notContains', 'pattern'."));
        }
        if (text.Pattern is not null)
        {
            try
            {
                _ = new Regex(text.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException e)
            {
                errors.Add(new FieldError($"{path}.pattern", $"The pattern is not a valid regular expression: {e.Message}"));
            }
        }
    }
}
