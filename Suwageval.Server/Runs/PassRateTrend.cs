using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>The points of an agent's pass-rate trend: which runs count and what each one's rates are.</summary>
internal static class PassRateTrend
{
    /// <summary>The trend shows at most this many runs, the newest.</summary>
    public const int MaxRuns = 50;

    /// <summary>
    /// One point of the trend: a run that called the agent (it gives the point its place and time) and, when it has been re-checked,
    /// its newest finished re-check, whose results the point shows.
    /// </summary>
    internal sealed record Point(RunRow Run, RunRow? RecheckedBy);

    /// <summary>
    /// The finished runs that called the agent among <paramref name="newestFirst"/>, at most <see cref="MaxRuns"/> of them, oldest
    /// first. A re-check is never a point of its own; the newest finished one of a run replaces that run's results.
    /// </summary>
    public static IReadOnlyList<Point> Pick(IReadOnlyList<RunRow> newestFirst)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        var latestRecheck = newestFirst
            .Where(run => run.State == RunStatus.Done && run.RecheckOf is not null)
            .GroupBy(run => run.RecheckOf!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        return
        [
            .. newestFirst
                .Where(run => run.State == RunStatus.Done && run.RecheckOf is null)
                .Take(MaxRuns)
                .Select(run => new Point(run, latestRecheck.GetValueOrDefault(run.Id)))
                .Reverse(),
        ];
    }

    /// <summary>
    /// Null when the run has no check. <paramref name="tagsOf"/> maps a scenario key to its tags; a scenario it does not know
    /// (deleted since) counts in the total only.
    /// </summary>
    public static (double PassRate, IReadOnlyList<TagPassRate> Tags)? Rates(
        RunResults results,
        IReadOnlyDictionary<string, IReadOnlyList<string>> tagsOf
    )
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(tagsOf);
        var checks = results
            .Scenarios.SelectMany(scenario =>
                scenario.Checks.Select(check => (Tags: tagsOf.GetValueOrDefault(scenario.Key) ?? [], check.Passed))
            )
            .ToList();
        if (checks.Count == 0)
        {
            return null;
        }
        var tags = checks
            .SelectMany(check => check.Tags)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .Select(tag => new TagPassRate(tag, Rate(checks.Where(check => check.Tags.Contains(tag)))))
            .ToList();
        return (Rate(checks), tags);
    }

    private static double Rate(IEnumerable<(IReadOnlyList<string> Tags, bool Passed)> checks)
    {
        var list = checks.ToList();
        return list.Count(check => check.Passed) / (double)list.Count;
    }
}
