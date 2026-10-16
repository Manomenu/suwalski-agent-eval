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
    /// True when the check fails, but its samples with an answer alone would reach the pass threshold (or there are none): only the samples
    /// where the agent gave no answer made it fail.
    /// </summary>
    private static bool OnlyAgentErrors(CheckRunResult check, HashSet<int> errored)
    {
        if (check.Passed || errored.Count == 0)
        {
            return false;
        }
        var evaluated = check.Samples.Where(sample => !errored.Contains(sample.Sample)).ToList();
        return evaluated.Count == 0 || RunReport.Reaches(evaluated.Count(sample => sample.Passed), evaluated.Count, check.PassThreshold);
    }

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
                var errored = scenario
                    .Samples.Where(sample => sample.Status == SampleStatus.Failed)
                    .Select(sample => sample.Sample)
                    .ToHashSet();
                var checks = scenario
                    .Checks.Select(check =>
                    {
                        var points = historyOf[(scenarioIdOf[scenario.Key], checkIdOf[(scenario.Key, check.Key)])]
                            .Select(row => new CheckHistoryPoint
                            {
                                RunId = row.RunId,
                                CreatedAt = row.CreatedAt,
                                Passed = RunReport.Reaches(row.SamplesPassed, row.Samples, row.PassThreshold),
                            })
                            .ToList();
                        return check with { Regression = RegressionStates.Of(points, n, check.Passed, OnlyAgentErrors(check, errored)) };
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
                AgentErrors = Count(RegressionState.AgentError),
            },
        };
    }
}
