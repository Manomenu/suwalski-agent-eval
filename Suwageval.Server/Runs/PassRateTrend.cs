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
    public static (double PassRate, double? MedianMs, double? P95Ms, IReadOnlyList<TagPassRate> Tags)? Rates(
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
            .Select(tag =>
            {
                var durations = Durations(
                    results.Scenarios.Where(scenario => (tagsOf.GetValueOrDefault(scenario.Key) ?? []).Contains(tag))
                );
                return new TagPassRate(tag, Rate(checks.Where(check => check.Tags.Contains(tag))), Median(durations), P95(durations));
            })
            .ToList();
        var all = Durations(results.Scenarios);
        return (Rate(checks), Median(all), P95(all), tags);
    }

    /// <summary>How long the agent took for each sample that made a call; a sample without a call has no duration.</summary>
    private static List<int> Durations(IEnumerable<ScenarioRunResult> scenarios) =>
        [
            .. scenarios
                .SelectMany(scenario => scenario.Samples)
                .Where(sample => sample.DurationMs is not null)
                .Select(sample => sample.DurationMs!.Value),
        ];

    /// <summary>The middle value, the mean of the two middle ones for an even count; null for no value.</summary>
    public static double? Median(IReadOnlyCollection<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return null;
        }
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    /// <summary>The 95th percentile by nearest rank: the value at rank ⌈0.95·n⌉ of the sorted values; null for no value.</summary>
    public static double? P95(IReadOnlyCollection<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Count == 0 ? null : values.Order().ElementAt(Math.Max(1, (int)Math.Ceiling(0.95 * values.Count)) - 1);
    }

    private static double Rate(IEnumerable<(IReadOnlyList<string> Tags, bool Passed)> checks)
    {
        var list = checks.ToList();
        return list.Count(check => check.Passed) / (double)list.Count;
    }
}
