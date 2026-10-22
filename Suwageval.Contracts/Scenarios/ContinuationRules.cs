using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Scenarios;

/// <summary>
/// Whether a scenario's <c>continuation</c> is valid, and whether a check fits it — written once for the CLI and the server.
/// </summary>
public static class ContinuationRules
{
    /// <summary>The most turns a scenario may ask for: every turn is an agent call and a model call, times the samples.</summary>
    public const int MaxTurns = 20;

    /// <summary>What is wrong with the scenario's continuation, each with its path under the scenario (<c>continuation.turns</c>).</summary>
    public static IReadOnlyList<(string Path, string Message)> Problems(ScenarioInput scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var continuation = scenario.Continuation;
        var problems = new List<(string, string)>();
        if (continuation.Turns is < 1 or > MaxTurns)
        {
            problems.Add(("continuation.turns", $"turns is from 1 to {MaxTurns}."));
        }
        if (continuation.SimulatedUser is { } user && string.IsNullOrWhiteSpace(user))
        {
            problems.Add(("continuation.simulatedUser", "simulatedUser describes the user in plain words; it is not empty."));
        }
        if (continuation.SimulatedUser is null && continuation.Turns > 1)
        {
            problems.Add(
                (
                    "continuation.turns",
                    "More than one turn needs continuation.simulatedUser: the simulated user writes the message that starts each further turn."
                )
            );
        }
        return problems;
    }

    /// <summary>
    /// What is wrong with a check of a scenario with <paramref name="continuation"/>, or null: a simulation goes another way in every
    /// sample, so a check of one never picks a turn by its number.
    /// </summary>
    public static string? CheckProblem(CheckInput check, Continuation continuation)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(continuation);
        return continuation.SimulatedUser is not null && check.Where?.Turn.Kind == TurnSelectorKind.Number
            ? "where.turn is 'any' or 'last' in a simulated scenario: the conversation goes another way in every sample, so turn numbers say nothing."
            : null;
    }
}
