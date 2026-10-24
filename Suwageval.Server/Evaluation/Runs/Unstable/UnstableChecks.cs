using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs.Unstable;

/// <summary>
/// Which checks are open unstable ones: the rule, over rows already read. A check (scenario, check key, revision) is unstable in a version
/// when it passed in some of the version's finished runs and failed in others (at least 2 runs). It is open, and listed, only while it is
/// unstable in the newest considered version that ran it in at least 2 runs: a newer version that ran it once proves nothing yet, one that
/// ran it twice or more without the problem closes it. A newer revision of the check, run at least twice in some version, closes the
/// older revisions.
/// </summary>
internal static class UnstableChecks
{
    private sealed record Tested(RunCheckVerdictRow First, int Rank, int Runs, int Passed, Guid NewestFailedRunId);

    /// <summary>
    /// The open unstable checks among the rows of finished runs, the share of failed runs first, then by keys. <paramref name="versionsNewestFirst"/>
    /// are the considered versions, newest first: their order decides which version is newer; rows of other versions are ignored.
    /// </summary>
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
