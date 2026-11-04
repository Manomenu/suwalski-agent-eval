using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs;

internal static class RegressionReport
{
    public static int HistoryOf(RegressionSettings? settings) => settings?.History ?? RegressionSettings.DefaultHistory;

    public static int RowsNeeded(int n) => 2 * n;

    private static bool OnlyAgentErrors(CheckRunResult check, HashSet<int> errored)
    {
        if (check.Passed || errored.Count == 0)
        {
            return false;
        }
        var evaluated = check.Samples.Where(sample => !errored.Contains(sample.Sample)).ToList();
        return evaluated.Count == 0 || RunReport.Reaches(evaluated.Count(sample => sample.Passed), evaluated.Count, check.PassThreshold);
    }

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
                    .Samples.Where(sample => sample.Status is SampleStatus.Failed or SampleStatus.NotSimulated)
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
                                DataVersion = row.DataVersion,
                            })
                            .ToList();
                        return check with
                        {
                            Regression = RegressionStates.Of(
                                points,
                                n,
                                check.Passed,
                                OnlyAgentErrors(check, errored),
                                results.Run.DataVersion
                            ),
                        };
                    })
                    .ToList();
                return scenario with { Checks = checks };
            })
            .ToList();

        // Why: Apply has just set Regression on every check of the results
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
