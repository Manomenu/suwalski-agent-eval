using System.Text.RegularExpressions;

namespace Suwageval.Contracts.Scenarios;

/// <summary>
/// Whether the <c>acGuardrail</c> of scenarios and the agent's <c>acGuardrailRetryLimit</c> are valid, written once for
/// <c>suwageval validate</c> and <c>push</c> and for the server when scenarios arrive.
/// </summary>
public static partial class AcGuardrailRules
{
    /// <summary>The attempts of a scenario when neither it nor the agent sets a limit.</summary>
    public const int DefaultRetryLimit = 5;

    public const int MinRetryLimit = 1;

    public const int MaxRetryLimit = 20;

    /// <summary>The attempts a scenario gets on the gate: its own limit, else the agent's, else <see cref="DefaultRetryLimit"/>.</summary>
    public static int EffectiveRetryLimit(int? scenario, int? agent) => scenario ?? agent ?? DefaultRetryLimit;

    /// <summary>The problem with a retry limit (not set is fine); <paramref name="path"/> is where it sits.</summary>
    public static string? RetryLimitProblem(int? retryLimit, string path) =>
        retryLimit is < MinRetryLimit or > MaxRetryLimit
            ? $"{path} is from {MinRetryLimit} to {MaxRetryLimit} (the default is {DefaultRetryLimit})."
            : null;

    /// <summary>The problems of one scenario's <c>acGuardrail</c>, with paths relative to the scenario.</summary>
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

    /// <summary>The message for an id another scenario of the agent already guards.</summary>
    public static string Duplicate(string id, string otherScenario) =>
        $"The acceptance criterion '{id}' is already guarded by the scenario '{otherScenario}'; one scenario guards one criterion.";

    /// <summary>The message for a guardrail scenario without checks: it would pass on any answer.</summary>
    public const string NoChecks =
        "A scenario with acGuardrail needs at least one check: without one it passes whatever the agent answers.";

    [GeneratedRegex("^AC-[a-z0-9]+(-[a-z0-9]+)*-[1-9][0-9]*$")]
    private static partial Regex IdPattern();
}
