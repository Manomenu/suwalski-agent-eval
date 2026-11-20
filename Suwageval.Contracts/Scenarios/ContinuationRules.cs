using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Scenarios;

public static class ContinuationRules
{
    public const int MaxTurns = 20;

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

    public static string? CheckProblem(CheckInput check, Continuation continuation)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(continuation);
        return continuation.SimulatedUser is not null && check.Where?.Turn.Kind == TurnSelectorKind.Number
            ? "where.turn is 'any' or 'last' in a simulated scenario: the conversation goes another way in every sample, so turn numbers say nothing."
            : null;
    }
}
