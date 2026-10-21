using System.Globalization;
using System.Text;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Variants;

/// <summary>What <c>suwageval variants</c> prints: a lettered header per run, the pass rate per tag, then a line per scenario with its checks per run and the failed ones.</summary>
internal static class VariantsText
{
    private const int CellWidth = 9;

    public static string Describe(RunVariants variants, Uri page)
    {
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(page);
        var text = new StringBuilder();
        text.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"variants of {variants.Agent}{(variants.Group is null ? "" : $" (group {variants.Group})")}, {variants.Runs.Count} runs"
            )
        );
        for (var column = 0; column < variants.Runs.Count; column++)
        {
            text.AppendLine(Header(column, variants.Runs[column]));
        }

        text.AppendLine().AppendLine("pass rate per tag:");
        var tagWidth = Math.Max(3, variants.Tags.Max(tag => tag.Tag?.Length ?? 3));
        foreach (var tag in variants.Tags)
        {
            text.Append("  ").Append((tag.Tag ?? "All").PadRight(tagWidth));
            foreach (var rate in tag.Rates)
            {
                var percent = rate.PassRate is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value * 100:0}%") : "-";
                text.Append("  ").Append(percent.PadRight(CellWidth));
            }
            text.AppendLine();
        }

        text.AppendLine().AppendLine("checks passed per scenario:");
        var keyWidth = Math.Max(8, variants.Scenarios.Max(scenario => scenario.Key.Length));
        foreach (var scenario in variants.Scenarios)
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
                    text.AppendLine($"    {Letter(column)} failed: {string.Join(", ", failing.FailedChecks)}");
                }
            }
        }
        text.Append("page: ").Append(page);
        return text.ToString();
    }

    private static string Header(int column, VariantRun variant)
    {
        var run = variant.Run;
        var name =
            !string.IsNullOrWhiteSpace(run.Label) ? run.Label
            : !string.IsNullOrWhiteSpace(variant.VersionLabel) ? variant.VersionLabel
            : run.AgentVersionId.ToString()[..8];
        var models = variant.ModelNames.Count == 0 ? "-" : string.Join(", ", variant.ModelNames);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Letter(column)}  {name}  models {models}  run {run.Id.ToString()[..8]}  checks {run.ChecksPassed}/{run.ChecksPassed + run.ChecksFailed} passed, {run.ChecksFailed} failed  samples {run.SamplesDone - run.SamplesFailed}/{run.SamplesTotal} answered  median {TurnTime.Format(variant.MedianMs)}  p95 {TurnTime.Format(variant.P95Ms)}"
        );
    }

    private static string Cell(VariantCell? cell) =>
        cell is null ? "-"
        : cell.ChecksTotal == 0 ? "no checks"
        : string.Create(CultureInfo.InvariantCulture, $"{cell.ChecksPassed}/{cell.ChecksTotal}");

    private static string Letter(int column) => ((char)('A' + column)).ToString();
}
