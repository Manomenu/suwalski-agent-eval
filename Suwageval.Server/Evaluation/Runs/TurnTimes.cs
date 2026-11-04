using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs;

internal static class TurnTimes
{
    public static List<int> Durations(IEnumerable<ScenarioRunResult> scenarios) =>
        [
            .. scenarios
                .SelectMany(scenario => scenario.Samples)
                .Where(sample => sample.DurationMs is not null)
                // Why: samples without a call are filtered out before, so a sample without per-turn times has a single duration
                .SelectMany(sample => sample.TurnDurationsMs ?? [sample.DurationMs!.Value]),
        ];

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

    public static double? P95(IReadOnlyCollection<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Count == 0 ? null : values.Order().ElementAt(Math.Max(1, (int)Math.Ceiling(0.95 * values.Count)) - 1);
    }
}
