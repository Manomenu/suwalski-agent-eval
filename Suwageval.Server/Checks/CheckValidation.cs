using System.Text.Json;
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
            switch (check)
            {
                case TextCheck text:
                    AddTextErrors(errors, text, path);
                    break;
                case ToolCalledCheck called:
                    AddMatcherErrors(errors, called.Args, $"{path}.args");
                    AddMatcherErrors(errors, called.Result, $"{path}.result");
                    break;
                case EventCheck emitted:
                    AddMatcherErrors(errors, emitted.Data, $"{path}.data");
                    break;
                case EventNotEmittedCheck notEmitted:
                    AddMatcherErrors(errors, notEmitted.Data, $"{path}.data");
                    AddNoJudgeErrors(errors, notEmitted.Data, $"{path}.data");
                    break;
                case CitedCheck cited:
                    AddCitedErrors(errors, cited, path);
                    break;
                case JudgeCheck judge when string.IsNullOrWhiteSpace(judge.Criteria):
                    errors.Add(new FieldError($"{path}.criteria", "A judge check needs 'criteria': what the agent's output must satisfy."));
                    break;
                default:
                    break;
            }
        }
        return errors;
    }

    /// <summary>The matchers in <c>args</c> / <c>result</c> / <c>data</c>: every <c>$pattern</c> compiles, every <c>$judge</c> is well formed.</summary>
    private static void AddMatcherErrors(List<FieldError> errors, JsonElement? expected, string path)
    {
        if (expected is not { } json)
        {
            return;
        }
        errors.AddRange(
            JsonSubset
                .InvalidPatterns(json, path)
                .Select(invalid => new FieldError(invalid.Path, $"The pattern is not a valid regular expression: {invalid.Error}"))
        );
        errors.AddRange(JudgeMatcher.Problems(json, path).Select(problem => new FieldError(problem.Path, problem.Message)));
    }

    /// <summary>"No event whose data a model would accept" would need a model call per event and turn its verdict upside down: not offered.</summary>
    private static void AddNoJudgeErrors(List<FieldError> errors, JsonElement? expected, string path)
    {
        if (expected is { } json && JudgeMatcher.IsIn(json))
        {
            errors.Add(
                new FieldError(
                    path,
                    $"'{JudgeMatcher.Key}' is not allowed in an event-not-emitted check; use a $pattern, or a literal value."
                )
            );
        }
    }

    private static void AddCitedErrors(List<FieldError> errors, CitedCheck cited, string path)
    {
        if (cited.Sources.Count == 0)
        {
            errors.Add(new FieldError($"{path}.sources", "A cited check needs at least one source."));
        }
        for (var i = 0; i < cited.Sources.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(cited.Sources[i]))
            {
                errors.Add(new FieldError($"{path}.sources[{i}]", "A source is not empty."));
            }
        }
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
