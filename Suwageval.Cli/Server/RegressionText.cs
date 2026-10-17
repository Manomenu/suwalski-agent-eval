using System.Globalization;
using System.Text;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Server;

/// <summary>
/// What <c>run --wait</c> prints about a run that has a regression summary: what it was compared with, then the checks that need attention
/// (regressions, new failures and agent errors first, then already broken, fixed and unstable ones), known failures only as a count, and the totals.
/// </summary>
internal static class RegressionText
{
    /// <summary>The width a section's heading is padded to when a note or a label follows it on the same line.</summary>
    private const int HeadingWidth = 36;

    public static string Describe(RunResults results)
    {
        var summary = results.Regression ?? throw new ArgumentException("The results have no regression summary.", nameof(results));
        var checks = results
            .Scenarios.SelectMany(scenario =>
                scenario.Checks.Select(check => (Scenario: scenario.Key, Label: $"{scenario.Key} / {check.Key}", Check: check))
            )
            .Where(item => item.Check.Regression is not null)
            .ToList();
        var agentErrors = results.Scenarios.ToDictionary(
            scenario => scenario.Key,
            scenario => scenario.Samples.FirstOrDefault(sample => sample.Status == SampleStatus.Failed)?.Error
        );
        var text = new StringBuilder();
        text.AppendLine(
            summary.ComparedWith is null
                ? "compared with every run of the agent (no git)"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"compared with {summary.ComparedWith} ({summary.History} reference runs make a steady pass; history up to {summary.History * 2})"
                )
        );

        var sections = new StringBuilder();
        Section(sections, checks, RegressionState.Regression, summary.Regressions, "✗", "regression", "regressions", withMessage: true);
        Section(sections, checks, RegressionState.New, summary.New, "✗", "new check failing", "new checks failing", withMessage: true);
        Section(
            sections,
            checks,
            RegressionState.AgentError,
            summary.AgentErrors,
            "✗",
            "agent error",
            "agent errors",
            detail: item => agentErrors[item.Scenario] ?? "the agent gave no answer"
        );
        Section(
            sections,
            checks,
            RegressionState.AlreadyBroken,
            summary.AlreadyBroken,
            "!",
            "already broken",
            "already broken",
            note: "not caused by this run",
            detail: item => item.Check.Regression?.FailingSince is { } since ? $"failing since run {Short(since)}" : null
        );
        Section(sections, checks, RegressionState.Fixed, summary.Fixed, "✓", "fixed", "fixed");
        Section(sections, checks, RegressionState.Unstable, summary.Unstable, "~", "unstable", "unstable");
        if (summary.KnownFailures > 0)
        {
            sections.AppendLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {summary.KnownFailures} known failure{(summary.KnownFailures == 1 ? "" : "s")}"
                )
            );
        }
        if (sections.Length > 0)
        {
            text.AppendLine().Append(sections);
        }
        text.AppendLine().Append(RunVerdict.Totals(results));
        return text.ToString();
    }

    private static void Section(
        StringBuilder text,
        List<(string Scenario, string Label, CheckRunResult Check)> checks,
        RegressionState state,
        int count,
        string mark,
        string singular,
        string plural,
        string? note = null,
        bool withMessage = false,
        Func<(string Scenario, string Label, CheckRunResult Check), string?>? detail = null
    )
    {
        if (count == 0)
        {
            return;
        }
        var items = checks.Where(item => item.Check.Regression?.State == state).ToList();
        var heading = string.Create(CultureInfo.InvariantCulture, $"{mark} {count} {(count == 1 ? singular : plural)}");
        // A section of one check with nothing to explain is one line: easier to read than a heading with a list under it.
        if (items.Count == 1 && !withMessage && detail is null)
        {
            text.AppendLine($"{heading.PadRight(HeadingWidth)}{items[0].Label}{Strip(items[0].Check, "  ")}");
            return;
        }
        text.AppendLine(note is null ? heading : heading.PadRight(HeadingWidth) + note);
        var width = items.Count == 0 ? 0 : items.Max(item => item.Label.Length);
        foreach (var item in items)
        {
            var why = withMessage ? FirstFailure(item.Check) : detail?.Invoke(item);
            text.AppendLine(Line(item.Label, width, Strip(item.Check, "  "), why));
        }
    }

    /// <summary>One check of a section: the label padded to the section's widest, so the strips line up, then the strip and the reason.</summary>
    private static string Line(string label, int width, string strip, string? why)
    {
        var line = strip.Length == 0 && why is null ? $"  {label}" : $"  {label.PadRight(width)}{strip}";
        return why is null ? line : $"{line}  {why}";
    }

    /// <summary>
    /// The check's history oldest first, then this run after an arrow: <c>✓✓✓✗ → ✗</c> (✓ passed, ✗ failed, – the agent gave no answer in this run). Empty, with no prefix,
    /// when the check has no history.
    /// </summary>
    internal static string Strip(CheckRunResult check, string prefix = "")
    {
        var history = check.Regression?.History ?? [];
        if (history.Count == 0)
        {
            return "";
        }
        var past = string.Concat(history.Select(point => point.Passed ? "✓" : "✗"));
        return $"{prefix}{past} → {(check.Regression?.State == RegressionState.AgentError ? "–" : check.Passed ? "✓" : "✗")}";
    }

    private static string? FirstFailure(CheckRunResult check) => check.Samples.FirstOrDefault(sample => !sample.Passed)?.Message;

    private static string Short(Guid id) => id.ToString()[..8];
}
