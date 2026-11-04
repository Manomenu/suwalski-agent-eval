using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Output;

internal static class RunVerdict
{
    public static int ExitCode(RunResults results, bool strict) =>
        results.Run.Status != RunStatus.Done ? 2
        : results.Regression is { } regression && !strict ? (regression.Regressions + regression.New + regression.AgentErrors > 0 ? 1 : 0)
        : results.Scenarios.All(scenario => scenario.Checks.All(check => check.Passed)) ? 0
        : 1;

    public static string Line(RunResults results)
    {
        var run = results.Run;
        if (run.Status == RunStatus.Interrupted)
        {
            return $"The run was interrupted: the server stopped during the run. Start a new run (`suwageval run`).";
        }
        if (run.Status != RunStatus.Done)
        {
            return $"The run {run.Status.ToString().ToLowerInvariant()}{(run.Error is null ? "." : $": {run.Error}")}";
        }
        return Totals(results);
    }

    public static string Totals(RunResults results)
    {
        var checks = results.Scenarios.SelectMany(scenario => scenario.Checks).ToList();
        var failed = checks.Count(check => !check.Passed);
        // Why: a judge whose model could not be asked or answered unreadably is not the same as one judged failed
        var notJudged = checks.Count(check => check.Samples.Any(sample => sample.NotJudged));
        var notSimulated = results.Scenarios.Sum(scenario => scenario.Samples.Count(sample => sample.Status == SampleStatus.NotSimulated));
        var unjudged =
            (notJudged == 0 ? "" : $" ({notJudged} not judged: the judge model could not give a verdict)")
            + (notSimulated == 0 ? "" : $" ({notSimulated} samples not simulated: the simulator model could not play the user)");
        if (results.Regression is not null)
        {
            return $"{checks.Count} checks: {checks.Count - failed} passed, {failed} failed{unjudged}";
        }
        return failed == 0
            ? $"{checks.Count} of {checks.Count} checks passed{unjudged}."
            : $"{failed} of {checks.Count} checks failed{unjudged}.";
    }
}
