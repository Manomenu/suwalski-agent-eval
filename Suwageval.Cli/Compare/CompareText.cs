using System.Globalization;
using System.Text;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;
using RunTrend = Suwageval.Contracts.Runs.Trend;

namespace Suwageval.Cli.Compare;

/// <summary>
/// What <c>suwageval compare</c> prints: a header per run (the first is the baseline), the pass rate per tag, then the checks whose
/// outcome differs between the runs and a count of the ones that do not.
/// </summary>
internal static class CompareText
{
    private const string Better = "▲";
    private const string Worse = "▼";
    private const int RateWidth = 20;

    public static string Describe(RunComparison comparison, Uri page)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(page);
        var text = new StringBuilder();
        for (var index = 0; index < comparison.Runs.Count; index++)
        {
            text.AppendLine(Header(index, comparison.Runs[index]));
        }

        text.AppendLine().AppendLine("pass rate per tag:");
        var width = Math.Max(3, comparison.Tags.Max(tag => tag.Tag?.Length ?? 3));
        foreach (var tag in comparison.Tags)
        {
            text.Append("  ").Append((tag.Tag ?? "All").PadRight(width));
            foreach (var rate in tag.Rates)
            {
                text.Append("  ").Append(Rate(rate).PadRight(RateWidth));
            }
            text.AppendLine();
        }

        var differing = comparison.Checks.Where(check => check.Differs).ToList();
        text.AppendLine();
        if (differing.Count > 0)
        {
            text.AppendLine("checks that differ:");
            foreach (var check in differing)
            {
                AppendCheck(text, check, comparison.Runs.Count);
            }
        }
        var same = comparison.Checks.Count - differing.Count;
        text.AppendLine(
            string.Create(CultureInfo.InvariantCulture, $"{same} {(same == 1 ? "check is" : "checks are")} the same in every run.")
        );
        text.Append("page: ").Append(page);
        return text.ToString();
    }

    private static string Header(int index, ComparedRun compared)
    {
        var run = compared.Run;
        var text = new StringBuilder();
        text.Append(
            string.Create(
                CultureInfo.InvariantCulture,
                $"#{index + 1}{(index == 0 ? " baseline" : "")}  run {Short(run.Id)}  version {Short(run.AgentVersionId)}  tags {RunTagsText.Of(run.Tags)}"
            )
        );
        if (compared.JudgeModel is not null)
        {
            text.Append("  judge ").Append(compared.JudgeModel);
            if (compared.JudgeChanged)
            {
                text.Append(" (judge differs from baseline)");
            }
        }
        text.Append("  pass ").Append(Percent(compared.PassRate));
        text.Append("  median ").Append(TurnTime.Format(compared.MedianMs)).Append(Time(compared.MedianChange));
        text.Append("  p95 ").Append(TurnTime.Format(compared.P95Ms)).Append(Time(compared.P95Change));
        return text.ToString();
    }

    private static string Rate(ComparedRate rate) =>
        rate.Change is { } change
            ? string.Create(CultureInfo.InvariantCulture, $"{Percent(rate.PassRate)} ({change.Points:+0.#;-0.#;0} pts{Mark(change.Trend)})")
            : Percent(rate.PassRate);

    private static string Time(TimeChange? change) =>
        change is { } value
            ? string.Create(CultureInfo.InvariantCulture, $" ({value.Milliseconds / 1000:+0.0;-0.0;0.0}s{Speed(value.Trend)})")
            : "";

    /// <summary>A turn time is better when lower, so it says faster or slower: an arrow would point the other way than for a pass rate.</summary>
    private static string Speed(RunTrend trend) =>
        trend switch
        {
            RunTrend.Better => " faster",
            RunTrend.Worse => " slower",
            RunTrend.Same => "",
            _ => "",
        };

    private static string Percent(double? rate) =>
        rate is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value * 100:0}%") : "-";

    private static string Mark(RunTrend trend) =>
        trend switch
        {
            RunTrend.Better => " " + Better,
            RunTrend.Worse => " " + Worse,
            RunTrend.Same => "",
            _ => "",
        };

    private static void AppendCheck(StringBuilder text, ComparedCheck check, int runs)
    {
        text.AppendLine(
            $"  {check.ScenarioKey} / {check.CheckKey}{(check.Comparable ? "" : "  (not comparable: some run has no such check)")}"
        );
        var baseline = check.Cells[0];
        for (var index = 0; index < runs; index++)
        {
            var cell = check.Cells[index];
            if (cell is null)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"    #{index + 1}  -"));
                continue;
            }
            var line = new StringBuilder();
            line.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"    #{index + 1}  {(cell.Passed ? "passed" : "failed")} {cell.SamplesPassed}/{cell.Samples} ({cell.PassRate * 100:0}%)"
                )
            );
            if (cell.Trend is { } trend)
            {
                line.Append(Mark(trend));
            }
            if (cell.NotJudged > 0)
            {
                line.Append(string.Create(CultureInfo.InvariantCulture, $", {cell.NotJudged} not judged"));
            }
            if (index > 0 && cell.JudgeModel is not null && cell.JudgeModel != baseline?.JudgeModel)
            {
                line.Append("  judge ").Append(cell.JudgeModel);
            }
            if (index > 0 && baseline is not null && cell.Revision != baseline.Revision)
            {
                line.Append(string.Create(CultureInfo.InvariantCulture, $"  revision {cell.Revision} (baseline {baseline.Revision})"));
            }
            text.AppendLine(line.ToString());
        }
    }

    private static string Short(Guid id) => id.ToString()[..8];
}
