using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs.Unstable;

internal static class UnstableChecks
{
    private sealed record Tested(RunCheckVerdictRow First, int Rank, int Runs, int Passed, Guid NewestFailedRunId);

    public static IReadOnlyList<UnstableCheck> Find(IEnumerable<RunCheckVerdictRow> rows, IReadOnlyList<Guid> versionsNewestFirst)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(versionsNewestFirst);
        var rank = versionsNewestFirst.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index);
        var tested = rows.Where(row => rank.ContainsKey(row.AgentVersionId))
            .GroupBy(row => (row.AgentVersionId, row.ScenarioKey, row.CheckKey, row.CheckRevision))
            .Where(group => group.Count() >= 2)
            .Select(group =>
            {
                var verdicts = group
                    .Select(row => (row.RunId, row.CreatedAt, Passed: RunReport.Reaches(row.SamplesPassed, row.Samples, row.PassThreshold)))
                    .ToList();
                var failed = verdicts.Where(verdict => !verdict.Passed).OrderByDescending(v => v.CreatedAt).ThenBy(v => v.RunId).ToList();
                return new Tested(
                    group.First(),
                    rank[group.Key.AgentVersionId],
                    verdicts.Count,
                    verdicts.Count - failed.Count,
                    failed.Count == 0 ? Guid.Empty : failed[0].RunId
                );
            })
            .ToList();
        return
        [
            .. tested
                .GroupBy(item => (item.First.ScenarioKey, item.First.CheckKey))
                .Select(check =>
                {
                    var latestRevision = check.Max(item => item.First.CheckRevision);
                    // Why: the group holds at least the item of the latest revision it was filtered by
                    return check.Where(item => item.First.CheckRevision == latestRevision).MinBy(item => item.Rank)!;
                })
                .Where(item => item.Passed > 0 && item.Passed < item.Runs)
                .OrderByDescending(item => (item.Runs - item.Passed) / (double)item.Runs)
                .ThenBy(item => item.First.ScenarioKey, StringComparer.Ordinal)
                .ThenBy(item => item.First.CheckKey, StringComparer.Ordinal)
                .ThenBy(item => item.First.CheckRevision)
                .ThenBy(item => item.First.AgentVersionId)
                .Select(item => new UnstableCheck
                {
                    ScenarioKey = item.First.ScenarioKey,
                    CheckKey = item.First.CheckKey,
                    CheckRevision = item.First.CheckRevision,
                    AgentVersionId = item.First.AgentVersionId,
                    Runs = item.Runs,
                    PassedRuns = item.Passed,
                    NewestFailedRunId = item.NewestFailedRunId,
                }),
        ];
    }
}
