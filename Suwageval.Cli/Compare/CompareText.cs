using System.Globalization;
using System.Text;
using Suwageval.Cli.Output;
using Suwageval.Contracts.Runs;
using RunTrend = Suwageval.Contracts.Runs.Trend;

namespace Suwageval.Cli.Compare;

/// <summary>
/// What <c>suwageval compare</c> prints: a lettered header per run, the pass rate per tag and a line per scenario with its checks per run
/// and the failed ones. With a baseline also the changes against it: the judge, the pass rate and the turn times of each run, the
/// changes of the pass rates and the checks whose outcome differs between the runs, with a count of the ones that do not.
/// </summary>
internal static class CompareText
{
    private const string Better = "▲";
    private const string Worse = "▼";
    private const int CellWidth = 9;
    private const int RateWithChangeWidth = 20;

    public static string Describe(RunComparison comparison, Uri page)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(page);
        var text = new StringBuilder();
        text.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"comparison of {comparison.Agent}{(comparison.Group is null ? "" : $" (group {comparison.Group})")}, {comparison.Runs.Count} {(comparison.Runs.Count == 1 ? "run" : "runs")}"
            )
        );
        for (var column = 0; column < comparison.Runs.Count; column++)
        {
            text.AppendLine(Header(column, comparison.Runs[column], comparison.Baseline?.RunId));
        }

        var baseline = comparison.Baseline;
        if (baseline is not null)
        {
            text.AppendLine().AppendLine("against the baseline:");
            for (var column = 0; column < baseline.Runs.Count; column++)
            {
                text.AppendLine(Against(column, baseline.Runs[column], baseline.RunId));
            }
        }

        text.AppendLine().AppendLine("pass rate per tag:");
        var tags = baseline?.Tags ?? comparison.Tags;
        var tagWidth = Math.Max(3, tags.Max(tag => tag.Tag?.Length ?? 3));
        var rateWidth = baseline is null ? CellWidth : RateWithChangeWidth;
        foreach (var tag in tags)
        {
            text.Append("  ").Append((tag.Tag ?? "All").PadRight(tagWidth));
            foreach (var rate in tag.Rates)
            {
                text.Append("  ").Append(Rate(rate).PadRight(rateWidth));
            }
            text.AppendLine();
        }

        text.AppendLine().AppendLine("checks passed per scenario:");
        var keyWidth = Math.Max(8, comparison.Scenarios.Max(scenario => scenario.Key.Length));
        foreach (var scenario in comparison.Scenarios)
        {
            text.Append("  ").Append(scenario.Key.PadRight(keyWidth));
            foreach (var cell in scenario.Cells)
            {
                text.Append("  ").Append(Cell(cell).PadRight(CellWidth));
            }
            text.AppendLine();
            for (var column = 0; column < scenario.Cells.Count; column++)
            {
                if (scenario.Cells[column] is { FailedChecks.Count: > 0 } failing)
                {
                    text.AppendLine(
                        $"    {Letter(column)} failed: {string.Join(", ", failing.FailedChecks.Select(key => FailedCheck(failing, key)))}"
                    );
                }
            }
        }

        if (baseline is not null)
        {
            AppendChecks(text, baseline);
        }
        text.Append("page: ").Append(page);
        return text.ToString();
    }

    /// <summary>A failed check's key, marked when a model judged it: its verdict may vary from run to run.</summary>
    private static string FailedCheck(VariantCell cell, string key) =>
        cell.Checks.Any(check => check.Key == key && check.JudgedByModel) ? $"{key} (model-judged)" : key;

    private static string Header(int column, VariantRun variant, Guid? baseline)
    {
        var run = variant.Run;
        var name =
            !string.IsNullOrWhiteSpace(run.Label) ? run.Label
            : !string.IsNullOrWhiteSpace(variant.VersionLabel) ? variant.VersionLabel
            : run.AgentVersionId.ToString()[..8];
        var models = variant.ModelNames.Count == 0 ? "-" : string.Join(", ", variant.ModelNames);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Letter(column)}{(run.Id == baseline ? " baseline" : "")}  {name}  models {models}  run {run.Id.ToString()[..8]}  checks {run.ChecksPassed}/{run.ChecksPassed + run.ChecksFailed} passed, {run.ChecksFailed} failed  samples {run.SamplesDone - run.SamplesFailed}/{run.SamplesTotal} answered  median {TurnTime.Format(variant.MedianMs)}  p95 {TurnTime.Format(variant.P95Ms)}{(variant.JudgeModel is null ? "" : $"  judge {variant.JudgeModel}")}"
        );
    }

    private static string Against(int column, ComparedRun compared, Guid baseline)
    {
        var text = new StringBuilder();
        text.Append(Letter(column)).Append(compared.Run.Id == baseline ? " baseline" : "");
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

    private static void AppendChecks(StringBuilder text, BaselineComparison baseline)
    {
        var differing = baseline.Checks.Where(check => check.Differs).ToList();
        text.AppendLine();
        if (differing.Count > 0)
        {
            text.AppendLine("checks that differ:");
            var baselineColumn = baseline.Runs.ToList().FindIndex(run => run.Run.Id == baseline.RunId);
            foreach (var check in differing)
            {
                AppendCheck(text, check, baselineColumn);
            }
        }
        var same = baseline.Checks.Count - differing.Count;
        text.AppendLine(
            string.Create(CultureInfo.InvariantCulture, $"{same} {(same == 1 ? "check is" : "checks are")} the same in every run.")
        );
    }

    private static string Cell(VariantCell? cell) =>
        cell is null ? "-"
        : cell.ChecksTotal == 0 ? "no checks"
        : string.Create(CultureInfo.InvariantCulture, $"{cell.ChecksPassed}/{cell.ChecksTotal}");

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

    private static void AppendCheck(StringBuilder text, ComparedCheck check, int baselineColumn)
    {
        text.AppendLine(
            $"  {check.ScenarioKey} / {check.CheckKey}{(check.Comparable ? "" : "  (not comparable: some run has no such check)")}"
        );
        var baseline = check.Cells[baselineColumn];
        for (var column = 0; column < check.Cells.Count; column++)
        {
            var cell = check.Cells[column];
            if (cell is null)
            {
                text.AppendLine($"    {Letter(column)}  -");
                continue;
            }
            var line = new StringBuilder();
            line.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"    {Letter(column)}  {(cell.Passed ? "passed" : "failed")} {cell.SamplesPassed}/{cell.Samples} ({cell.PassRate * 100:0}%)"
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
            if (column != baselineColumn && cell.JudgeModel is not null && cell.JudgeModel != baseline?.JudgeModel)
            {
                line.Append("  judge ").Append(cell.JudgeModel);
            }
            if (column != baselineColumn && baseline is not null && cell.Revision != baseline.Revision)
            {
                line.Append(string.Create(CultureInfo.InvariantCulture, $"  revision {cell.Revision} (baseline {baseline.Revision})"));
            }
            text.AppendLine(line.ToString());
        }
    }

    private static string Letter(int column) => ((char)('A' + column)).ToString();
}
