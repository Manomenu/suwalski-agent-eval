using System.Globalization;
using System.Text;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Results;

/// <summary>
/// What <c>suwageval results</c> prints for a run: a header (status, version, tag, branch), what <c>run --wait</c> prints (the regression
/// summary, else the totals), every failing check with the reason of its first failing sample, and the turn times.
/// </summary>
internal static class ResultsText
{
    public static string Describe(RunResults results, Uri page)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(page);
        var run = results.Run;
        var text = new StringBuilder();
        text.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"run {run.Id}  {run.Status.ToString().ToLowerInvariant()}{(run.Reference ? "  reference" : "")}"
            )
        );
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"agent {run.Agent}  version {run.AgentVersionId}"));
        text.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"tag {run.Tag ?? "-"}  branch {run.Origin?.Branch ?? "-"}  created {run.CreatedAt:yyyy-MM-dd HH:mm}Z"
            )
        );
        text.AppendLine()
            .AppendLine(
                run.Status == RunStatus.Done && results.Regression is not null ? RegressionText.Describe(results) : RunVerdict.Line(results)
            );

        var failing = results
            .Scenarios.SelectMany(scenario => scenario.Checks.Where(check => !check.Passed).Select(check => (scenario.Key, check)))
            .ToList();
        if (failing.Count > 0)
        {
            text.AppendLine().AppendLine("failing checks:");
            foreach (var (scenario, check) in failing)
            {
                var sample = check.Samples.FirstOrDefault(sample => !sample.Passed);
                text.AppendLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {scenario} / {check.Key}  {check.PassRate * 100:0}% passed, needs {check.PassThreshold * 100:0}%{(check.JudgeModel is null ? "" : $"  judge {check.JudgeModel}")}"
                    )
                );
                if (sample is not null)
                {
                    text.AppendLine(
                        string.Create(CultureInfo.InvariantCulture, $"    sample {sample.Sample}: {sample.Message.ReplaceLineEndings(" ")}")
                    );
                }
            }
        }

        text.AppendLine().AppendLine($"turn time: median {TurnTime.Format(results.MedianMs)}, p95 {TurnTime.Format(results.P95Ms)}");
        text.Append(page);
        return text.ToString();
    }
}
