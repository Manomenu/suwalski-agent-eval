using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>Puts the regression states of a finished run's checks into its results.</summary>
internal static class RegressionReport
{
    /// <summary>How many of the newest reference runs a check's history is made of (N) for an agent with these settings.</summary>
    public static int HistoryOf(RegressionSettings? settings) => settings?.History ?? RegressionSettings.DefaultHistory;

    /// <summary>How many runs of history the query asks for: N to judge the last N runs plus N more to tell a failure that began from one that is old.</summary>
    public static int RowsNeeded(int n) => 2 * n;

    /// <summary>
    /// <paramref name="results"/> with <see cref="CheckRunResult.Regression"/> on every check and <see cref="RunResults.Regression"/>.
    /// <paramref name="history"/> comes from <see cref="RunStore.ListCheckHistoriesAsync"/> for the same run.
    /// </summary>
    public static RunResults Apply(
        RunResults results,
        IReadOnlyList<ScenarioResultRow> samples,
        IReadOnlyList<CheckResultRow> verdicts,
        IReadOnlyList<RegressionHistoryRow> history,
        int n
    )
    {
        ArgumentNullException.ThrowIfNull(results);
        var scenarioIdOf = samples
            .GroupBy(sample => sample.ScenarioKey)
            .ToDictionary(group => group.Key, group => group.First().ScenarioId);
        var scenarioKeyOf = samples.ToDictionary(sample => sample.Id, sample => sample.ScenarioKey);
        var checkIdOf = verdicts
            .GroupBy(verdict => (Scenario: scenarioKeyOf[verdict.ScenarioResultId], verdict.CheckKey))
            .ToDictionary(group => group.Key, group => group.First().CheckId);
        var historyOf = history.ToLookup(row => (row.ScenarioId, row.CheckId));

        var scenarios = results
            .Scenarios.Select(scenario =>
            {
                var agentFailed = scenario.Samples.Any(sample => sample.Error is not null);
                var checks = scenario
                    .Checks.Select(check =>
                    {
                        var points = historyOf[(scenarioIdOf[scenario.Key], checkIdOf[(scenario.Key, check.Key)])]
                            .Select(row => new RegressionStates.Point(
                                row.RunId,
                                RunReport.Reaches(row.SamplesPassed, row.Samples, row.PassThreshold)
                            ))
                            .ToList();
                        return check with { Regression = RegressionStates.Of(points, n, check.Passed, agentFailed) };
                    })
                    .ToList();
                return scenario with { Checks = checks };
            })
            .ToList();

        var states = scenarios.SelectMany(scenario => scenario.Checks).Select(check => check.Regression!.State).ToList();
        var count = states.GroupBy(state => state).ToDictionary(group => group.Key, group => group.Count());
        int Count(RegressionState state)
        {
            return count.GetValueOrDefault(state);
        }

        return results with
        {
            Scenarios = scenarios,
            Regression = new RegressionSummary
            {
                ComparedWith = results.Run.ComparedWith,
                History = n,
                Regressions = Count(RegressionState.Regression),
                New = Count(RegressionState.New),
                AlreadyBroken = Count(RegressionState.AlreadyBroken),
                KnownFailures = Count(RegressionState.KnownFailure),
                Fixed = Count(RegressionState.Fixed),
                Unstable = Count(RegressionState.Unstable),
            },
        };
    }
}
