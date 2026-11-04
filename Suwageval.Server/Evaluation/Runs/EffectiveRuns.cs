using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs;

// Why: a group run is an experiment and a gate run is a verdict, so neither counts, and a re-check replaces the verdicts of its original instead of being a sample
internal static class EffectiveRuns
{
    internal sealed record Point(RunRow Run, RunRow? RecheckedBy)
    {
        public RunRow Shown => RecheckedBy ?? Run;
    }

    public static IReadOnlyList<Point> Pick(IReadOnlyList<RunRow> newestFirst, int maxRuns)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        var latestRecheck = newestFirst
            .Where(run => run.State == RunStatus.Done && run.RecheckOf is not null)
            // Why: the runs were filtered to re-checks just above, so RecheckOf is set
            .GroupBy(run => run.RecheckOf!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        return
        [
            .. newestFirst
                .Where(run => run.State == RunStatus.Done && run.RecheckOf is null && run.RunGroup is null && !run.Gate)
                .Take(maxRuns)
                .Select(run => new Point(run, latestRecheck.GetValueOrDefault(run.Id)))
                .Reverse(),
        ];
    }
}
