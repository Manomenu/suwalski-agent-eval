using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Server;

/// <summary>The verdict of a finished run: the exit code and the one line a terminal or a CI log gets (the rest is on the web).</summary>
internal static class RunVerdict
{
    /// <summary>0 when every check reached its pass threshold (a run with no checks has nothing to fail), 1 when one did not, 2 when the run did not finish.</summary>
    public static int ExitCode(RunResults results) =>
        results.Run.Status != RunStatus.Done ? 2
        : results.Scenarios.All(scenario => scenario.Checks.All(check => check.Passed)) ? 0
        : 1;

    public static string Line(RunResults results)
    {
        var run = results.Run;
        if (run.Status != RunStatus.Done)
        {
            return $"The run {run.Status.ToString().ToLowerInvariant()}{(run.Error is null ? "." : $": {run.Error}")}";
        }
        var checks = results.Scenarios.SelectMany(scenario => scenario.Checks).ToList();
        var failed = checks.Count(check => !check.Passed);
        return failed == 0 ? $"{checks.Count} of {checks.Count} checks passed." : $"{failed} of {checks.Count} checks failed.";
    }
}
