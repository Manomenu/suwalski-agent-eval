using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs.Trend;

internal static class PassRateTrend
{
    public const int MaxRuns = 50;

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
