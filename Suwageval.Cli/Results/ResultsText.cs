using System.Globalization;
using System.Text;
using Suwageval.Cli.Output;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Results;

/// <summary>
/// What <c>suwageval results</c> prints for a run: a header (status, version, tags, branch), what <c>run --wait</c> prints (the regression
/// summary, else the totals), every failing check with the reason of its first failing sample, and the turn times. With
/// <c>failedOnly</c> the failing checks give way to everything that did not pass (see <c>PassedFully</c> on the scenario and the check results) and a line of how much was left out.
/// </summary>
internal static class ResultsText
{
    public static string Describe(RunResults results, Uri page, bool failedOnly = false)
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
        text.AppendLine(
            string.Create(CultureInfo.InvariantCulture, $"agent {run.Agent}  version {run.AgentVersionId}{RunLabelText.Suffix(run.Label)}")
        );
        text.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"tags {RunTagsText.Of(run.Tags)}  branch {run.Origin?.Branch ?? "-"}  created {run.CreatedAt:yyyy-MM-dd HH:mm}Z"
            )
        );
        text.AppendLine()
            .AppendLine(
                run.Status == RunStatus.Done && results.Regression is not null ? RegressionText.Describe(results) : RunVerdict.Line(results)
            );

        if (failedOnly)
        {
            AppendNotPassed(text, results);
        }
        else
        {
            AppendFailing(text, results);
        }

        text.AppendLine().AppendLine($"turn time: median {TurnTime.Format(results.MedianMs)}, p95 {TurnTime.Format(results.P95Ms)}");
        text.Append(page);
        return text.ToString();
    }

    private static void AppendFailing(StringBuilder text, RunResults results)
    {
        var failing = results
            .Scenarios.SelectMany(scenario => scenario.Checks.Where(check => !check.Passed).Select(check => (scenario.Key, check)))
            .ToList();
        if (failing.Count == 0)
        {
            return;
        }
        text.AppendLine().AppendLine("failing checks:");
        foreach (var (scenario, check) in failing)
        {
            AppendCheck(text, scenario, check);
        }
    }

    private static void AppendNotPassed(StringBuilder text, RunResults results)
    {
        var left = results.Scenarios.Where(scenario => !scenario.PassedFully).ToList();
        if (left.Count > 0)
        {
            text.AppendLine().AppendLine("not passed:");
        }
        foreach (var scenario in left)
        {
            foreach (var sample in scenario.Samples.Where(sample => sample.Status != SampleStatus.Done))
            {
                text.AppendLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {scenario.Key}  sample {sample.Sample}: {(sample.Status == SampleStatus.Failed ? "no answer" : "simulated user not played")}: {sample.Error?.ReplaceLineEndings(" ") ?? "no error given"}"
                    )
                );
            }
            foreach (var check in scenario.Checks.Where(check => !check.PassedFully))
            {
                AppendCheck(text, scenario.Key, check);
            }
        }
        var passedChecks = results.Scenarios.Sum(scenario => scenario.Checks.Count(check => check.PassedFully));
        var passedScenarios = results.Scenarios.Count(scenario => scenario.PassedFully);
        text.AppendLine()
            .AppendLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"left out: {passedChecks} passed {(passedChecks == 1 ? "check" : "checks")}{(passedScenarios == 0 ? "" : $" ({passedScenarios} {(passedScenarios == 1 ? "scenario" : "scenarios")} passed entirely)")}; the samples and checks totals above count the whole run"
                )
            );
    }

    private static void AppendCheck(StringBuilder text, string scenario, CheckRunResult check)
    {
        var notJudged = check.Samples.Count(sample => sample.NotJudged);
        text.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"  {scenario} / {check.Key}  {check.PassRate * 100:0}% passed, needs {check.PassThreshold * 100:0}%{(check.JudgeModel is null ? "" : $"  judge {check.JudgeModel}")}{(notJudged > 0 ? $"  not judged: {notJudged} of {check.Samples.Count} samples" : "")}"
            )
        );
        var sample = check.Samples.FirstOrDefault(sample => !sample.Passed);
        if (sample is not null)
        {
            text.AppendLine(
                string.Create(CultureInfo.InvariantCulture, $"    sample {sample.Sample}: {sample.Message.ReplaceLineEndings(" ")}")
            );
        }
    }
}
