using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>The points of an agent's pass-rate trend: which runs count and what each one's rates are.</summary>
internal static class PassRateTrend
{
    /// <summary>The trend shows at most this many runs, the newest.</summary>
    public const int MaxRuns = 50;

    /// <summary>
    /// Null when the run has no check. <paramref name="verdicts"/> is one entry per check of a scenario (passed by its threshold),
    /// <paramref name="durations"/> the turn time of every sample that made a call, both with the scenario's key;
    /// <paramref name="tagsOf"/> maps a scenario key to its tags; a scenario it does not know counts in the total only. A tag gets a rate
    /// only when it is among <paramref name="completeTags"/>: a run that ran just part of a tag's scenarios says nothing about the tag.
    /// </summary>
    public static (double PassRate, double? MedianMs, double? P95Ms, IReadOnlyList<TagPassRate> Tags)? Rates(
        IReadOnlyList<(string ScenarioKey, bool Passed)> verdicts,
        IReadOnlyList<(string ScenarioKey, int DurationMs)> durations,
        IReadOnlyDictionary<string, IReadOnlyList<string>> tagsOf,
        IReadOnlySet<string> completeTags
    )
    {
        ArgumentNullException.ThrowIfNull(completeTags);
        ArgumentNullException.ThrowIfNull(verdicts);
        ArgumentNullException.ThrowIfNull(durations);
        ArgumentNullException.ThrowIfNull(tagsOf);
        var checks = verdicts.Select(check => (Tags: tagsOf.GetValueOrDefault(check.ScenarioKey) ?? [], check.Passed)).ToList();
        if (checks.Count == 0)
        {
            return null;
        }
        var tags = checks
            .SelectMany(check => check.Tags)
            .Distinct()
            .Where(completeTags.Contains)
            .Order(StringComparer.Ordinal)
            .Select(tag =>
            {
                List<int> ofTag =
                [
                    .. durations
                        .Where(duration => (tagsOf.GetValueOrDefault(duration.ScenarioKey) ?? []).Contains(tag))
                        .Select(duration => duration.DurationMs),
                ];
                return new TagPassRate(
                    tag,
                    Rate(checks.Where(check => check.Tags.Contains(tag))),
                    TurnTimes.Median(ofTag),
                    TurnTimes.P95(ofTag)
                );
            })
            .ToList();
        List<int> all = [.. durations.Select(duration => duration.DurationMs)];
        return (Rate(checks), TurnTimes.Median(all), TurnTimes.P95(all), tags);
    }

    private static double Rate(IEnumerable<(IReadOnlyList<string> Tags, bool Passed)> checks)
    {
        var list = checks.ToList();
        return list.Count(check => check.Passed) / (double)list.Count;
    }
}
