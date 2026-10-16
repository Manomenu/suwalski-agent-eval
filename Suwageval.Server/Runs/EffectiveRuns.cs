using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>
/// The one rule for whose verdicts a re-checked run shows: every run that called the agent counts once, and when it has been
/// re-checked, its newest finished re-check supplies the verdicts (the re-check judges the same stored answers with the current
/// checks, so it replaces the original's verdicts and is never a sample of its own). The pass-rate trend and the unstable checks both read runs through it.
/// </summary>
internal static class EffectiveRuns
{
    /// <summary>
    /// A run that called the agent (it gives the point its place and time) and, when it has been re-checked, its newest finished
    /// re-check, whose results the point shows.
    /// </summary>
    internal sealed record Point(RunRow Run, RunRow? RecheckedBy)
    {
        /// <summary>The run whose verdicts count.</summary>
        public RunRow Shown => RecheckedBy ?? Run;
    }

    /// <summary>
    /// The finished runs that called the agent among <paramref name="newestFirst"/>, at most <paramref name="maxRuns"/> of them, oldest
    /// first. A re-check is never a point of its own; the newest finished one of a run replaces that run's results.
    /// </summary>
    public static IReadOnlyList<Point> Pick(IReadOnlyList<RunRow> newestFirst, int maxRuns)
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
                .Take(maxRuns)
                .Select(run => new Point(run, latestRecheck.GetValueOrDefault(run.Id)))
                .Reverse(),
        ];
    }
}
