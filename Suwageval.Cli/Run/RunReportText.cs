using System.Globalization;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Run;

/// <summary>The results of a run written for a person, and the verdict that becomes the exit code.</summary>
internal static class RunReportText
{
    /// <summary>True when every check reached its pass threshold (a run with no checks has nothing to fail).</summary>
    public static bool AllPassed(RunResults results) => results.Scenarios.All(scenario => scenario.Checks.All(check => check.Passed));

    public static string Format(RunResults results)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var run = results.Run;
        writer.WriteLine(
            $"run {run.Id}  {run.Status}  (agent {run.Agent}, version {run.AgentVersionId}{(run.Tag is null ? "" : $", tag {run.Tag}")}, {run.SamplesDone}/{run.SamplesTotal} samples)"
        );
        if (run.ContractChangedDuringRun)
        {
            writer.WriteLine("! the agent's contract changed while the run was going: some samples may not be of this version.");
        }
        var checks = 0;
        var passed = 0;
        foreach (var scenario in results.Scenarios)
        {
            writer.WriteLine();
            writer.WriteLine(scenario.Key);
            foreach (var sample in scenario.Samples.Where(sample => sample.Status == SampleStatus.Failed))
            {
                writer.WriteLine($"  ! sample {sample.Sample} produced nothing: {sample.Error}");
            }
            foreach (var sample in scenario.Samples.Where(sample => sample.Undeclared.Count > 0))
            {
                writer.WriteLine(
                    $"  ! sample {sample.Sample} produced what its version does not declare: {string.Join(", ", sample.Undeclared)}"
                );
            }
            if (scenario.Checks.Count == 0)
            {
                writer.WriteLine("  (no checks)");
            }
            foreach (var check in scenario.Checks)
            {
                checks++;
                passed += check.Passed ? 1 : 0;
                var passes = check.Samples.Count(sample => sample.Passed);
                writer.WriteLine(
                    $"  {(check.Passed ? "✓" : "✗")} {check.Key}  {Percent(check.PassRate)} ({passes}/{check.Samples.Count} samples, needs {Percent(check.PassThreshold)})"
                );
                foreach (var sample in check.Samples.Where(sample => !sample.Passed || check.Samples.Count == 1))
                {
                    writer.WriteLine($"      {(check.Samples.Count > 1 ? $"sample {sample.Sample}: " : "")}{sample.Message}");
                }
            }
        }
        writer.WriteLine();
        writer.WriteLine($"{passed} of {checks} checks passed.");
        return writer.ToString();
    }

    private static string Percent(double share) => (share * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
