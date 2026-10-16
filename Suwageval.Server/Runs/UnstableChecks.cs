using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>Which checks are unstable: the rule, over rows already read.</summary>
internal static class UnstableChecks
{
    /// <summary>The unstable checks among the rows of finished runs, the share of failed runs first, then by keys.</summary>
    public static IReadOnlyList<UnstableCheck> Find(IEnumerable<RunCheckVerdictRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return
        [
            .. rows.GroupBy(row => (row.AgentVersionId, row.ScenarioKey, row.CheckKey, row.CheckRevision))
                .Select(group =>
                {
                    var verdicts = group
                        .Select(row =>
                            (row.RunId, row.CreatedAt, Passed: RunReport.Reaches(row.SamplesPassed, row.Samples, row.PassThreshold))
                        )
                        .ToList();
                    var passed = verdicts.Count(verdict => verdict.Passed);
                    return (Group: group, Runs: verdicts.Count, Passed: passed, Verdicts: verdicts);
                })
                .Where(item => item.Runs >= 2 && item.Passed > 0 && item.Passed < item.Runs)
                .OrderByDescending(item => (item.Runs - item.Passed) / (double)item.Runs)
                .ThenBy(item => item.Group.Key.ScenarioKey, StringComparer.Ordinal)
                .ThenBy(item => item.Group.Key.CheckKey, StringComparer.Ordinal)
                .ThenBy(item => item.Group.Key.CheckRevision)
                .ThenBy(item => item.Group.Key.AgentVersionId)
                .Select(item => new UnstableCheck
                {
                    ScenarioKey = item.Group.Key.ScenarioKey,
                    CheckKey = item.Group.Key.CheckKey,
                    CheckRevision = item.Group.Key.CheckRevision,
                    AgentVersionId = item.Group.Key.AgentVersionId,
                    Runs = item.Runs,
                    PassedRuns = item.Passed,
                    NewestFailedRunId = item
                        .Verdicts.Where(verdict => !verdict.Passed)
                        .OrderByDescending(verdict => verdict.CreatedAt)
                        .ThenBy(verdict => verdict.RunId)
                        .First()
                        .RunId,
                }),
        ];
    }
}
