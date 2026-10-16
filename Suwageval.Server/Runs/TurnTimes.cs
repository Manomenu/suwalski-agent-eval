using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>How long the agent took to answer a turn: the samples' durations and the statistics the run report and the trend show.</summary>
internal static class TurnTimes
{
    /// <summary>How long the agent took for each sample that made a call; a sample without a call has no duration.</summary>
    public static List<int> Durations(IEnumerable<ScenarioRunResult> scenarios) =>
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
}
