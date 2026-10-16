using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Server;

/// <summary>The verdict of a finished run: the exit code and the one line a terminal or a CI log gets (the rest is on the web).</summary>
internal static class RunVerdict
{
    /// <summary>
    /// 2 when the run did not finish. Otherwise, with the regression summary and without <paramref name="strict"/>: 1 when a check regressed, is
    /// new and failing or failed because the agent gave no answer, else 0 (known failures, unstable and already broken checks do not fail it). Strict, or without the summary: 0 when every
    /// check reached its pass threshold (a run with no checks has nothing to fail), 1 when one did not.
    /// </summary>
    public static int ExitCode(RunResults results, bool strict) =>
        results.Run.Status != RunStatus.Done ? 2
        : results.Regression is { } regression && !strict ? (regression.Regressions + regression.New + regression.AgentErrors > 0 ? 1 : 0)
        : results.Scenarios.All(scenario => scenario.Checks.All(check => check.Passed)) ? 0
        : 1;

    public static string Line(RunResults results)
    {
        var run = results.Run;
        if (run.Status != RunStatus.Done)
        {
            return $"The run {run.Status.ToString().ToLowerInvariant()}{(run.Error is null ? "." : $": {run.Error}")}";
        }
        return Totals(results);
    }

    /// <summary>The totals line of a finished run: how many checks passed, and how many a judge model could not decide.</summary>
    public static string Totals(RunResults results)
    {
        var checks = results.Scenarios.SelectMany(scenario => scenario.Checks).ToList();
        var failed = checks.Count(check => !check.Passed);
        // A judge check whose model could not be asked or answered unreadably is not the same as one judged failed.
        var notJudged = checks.Count(check => check.Samples.Any(sample => sample.NotJudged));
        var unjudged = notJudged == 0 ? "" : $" ({notJudged} not judged: the judge model could not give a verdict)";
        if (results.Regression is not null)
        {
            return $"{checks.Count} checks: {checks.Count - failed} passed, {failed} failed{unjudged}";
        }
        return failed == 0
            ? $"{checks.Count} of {checks.Count} checks passed{unjudged}."
            : $"{failed} of {checks.Count} checks failed{unjudged}.";
    }
}
