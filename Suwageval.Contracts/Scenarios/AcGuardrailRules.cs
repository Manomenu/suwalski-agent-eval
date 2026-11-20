using System.Text.RegularExpressions;

namespace Suwageval.Contracts.Scenarios;

public static partial class AcGuardrailRules
{
    public const int DefaultRetryLimit = 5;

    public const int MinRetryLimit = 1;

    public const int MaxRetryLimit = 20;

    public static int EffectiveRetryLimit(int? scenario, int? agent) => scenario ?? agent ?? DefaultRetryLimit;

    public static string? RetryLimitProblem(int? retryLimit, string path) =>
        retryLimit is < MinRetryLimit or > MaxRetryLimit
            ? $"{path} is from {MinRetryLimit} to {MaxRetryLimit} (the default is {DefaultRetryLimit})."
            : null;

    public static IReadOnlyList<(string Path, string Message)> Problems(AcGuardrail? guardrail)
    {
        if (guardrail is null)
        {
            return [];
        }
        var problems = new List<(string Path, string Message)>();
        if (!IdPattern().IsMatch(guardrail.Id))
        {
            problems.Add(
                (
                    "acGuardrail.id",
                    $"The acceptance criterion id '{guardrail.Id}' is not AC-<feature>-<n> (like 'AC-refunds-2': a lowercase kebab-case feature and a number from 1)."
                )
            );
        }
        if (RetryLimitProblem(guardrail.RetryLimit, "acGuardrail.retryLimit") is { } limit)
        {
            problems.Add(("acGuardrail.retryLimit", limit));
        }
        return problems;
    }

    public static string Duplicate(string id, string otherScenario) =>
        $"The acceptance criterion '{id}' is already guarded by the scenario '{otherScenario}'; one scenario guards one criterion.";

    public const string NoChecks =
        "A scenario with acGuardrail needs at least one check: without one it passes whatever the agent answers.";

    [GeneratedRegex("^AC-[a-z0-9]+(-[a-z0-9]+)*-[1-9][0-9]*$")]
    private static partial Regex IdPattern();
}
