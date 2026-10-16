using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>
/// A check's verdict in a run against its history: the history is the baseline, nobody marks one. The state follows from the check's
/// outcomes over the newest reference runs (oldest first) and whether it passed now.
/// </summary>
internal static class RegressionStates
{
    /// <summary>One earlier run of a check's history and whether the check passed in it.</summary>
    internal readonly record struct Point(Guid RunId, bool Passed);

    private enum Baseline
    {
        StablePass,
        Known,
        Broken,
        NoHistory,
        Mixed,
    }

    /// <summary>
    /// The state of a check that <paramref name="passed"/> now, with <paramref name="history"/> (oldest first, at most the newest 2N) and a
    /// history of <paramref name="n"/> runs: a steady pass that fails now is a <see cref="RegressionState.Regression"/>; a failure in a row
    /// for N runs is known, a shorter one after N passes is already broken; a mixed past is unstable; with too little history a failure is new.
    /// </summary>
    public static CheckRegression Of(IReadOnlyList<Point> history, int n, bool passed, bool agentFailed)
    {
        var outcomes = history.Select(point => point.Passed).ToList();
        var state = State(outcomes, n, passed);
        var trailing = TrailingFailures(outcomes);
        return new CheckRegression
        {
            State = state,
            FailingSince = state == RegressionState.AlreadyBroken ? history[^trailing].RunId : null,
            LastPassedRun = history.Any(point => point.Passed) ? history.Last(point => point.Passed).RunId : null,
            AgentFailed = agentFailed,
        };
    }

    public static RegressionState State(IReadOnlyList<bool> history, int n, bool passed) =>
        (BaselineOf(history, n), passed) switch
        {
            (Baseline.StablePass, true) => RegressionState.Passed,
            (Baseline.StablePass, false) => RegressionState.Regression,
            (Baseline.Broken, true) => RegressionState.Fixed,
            (Baseline.Broken, false) => RegressionState.AlreadyBroken,
            (Baseline.Known, true) => RegressionState.Fixed,
            (Baseline.Known, false) => RegressionState.KnownFailure,
            (Baseline.NoHistory, true) => RegressionState.Passed,
            (Baseline.NoHistory, false) => RegressionState.New,
            _ => RegressionState.Unstable,
        };

    private static Baseline BaselineOf(IReadOnlyList<bool> history, int n)
    {
        var trailing = TrailingFailures(history);
        if (trailing == 0 && history.Count >= n && history.Skip(history.Count - n).All(passed => passed))
        {
            return Baseline.StablePass;
        }
        if (trailing >= n)
        {
            return Baseline.Known;
        }
        if (trailing >= 1 && history.Count - trailing >= n && history.Skip(history.Count - trailing - n).Take(n).All(passed => passed))
        {
            return Baseline.Broken;
        }
        return history.Count < n ? Baseline.NoHistory : Baseline.Mixed;
    }

    private static int TrailingFailures(IReadOnlyList<bool> history)
    {
        var count = 0;
        while (count < history.Count && !history[history.Count - 1 - count])
        {
            count++;
        }
        return count;
    }
}
