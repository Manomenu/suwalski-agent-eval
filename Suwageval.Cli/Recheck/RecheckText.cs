using System.Globalization;
using System.Text;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Recheck;

/// <summary>A re-check plan in words: what would be evaluated again, what is left out, or why nothing can start.</summary>
internal static class RecheckText
{
    public static string Describe(Guid runId, RecheckPlan plan)
    {
        var text = new StringBuilder();
        if (plan.Possible)
        {
            var judged = plan.JudgedChecks == 0 ? "" : $" ({plan.JudgedChecks} judged by a model)";
            text.Append(
                CultureInfo.InvariantCulture,
                $"Re-checking run {runId} evaluates {plan.ChangedChecks.Count} checks again{judged}:"
            );
        }
        else
        {
            text.Append(CultureInfo.InvariantCulture, $"Run {runId} cannot be re-checked: {plan.Reason}");
        }
        foreach (var change in plan.ChangedChecks)
        {
            text.Append(
                CultureInfo.InvariantCulture,
                $"\n  {change.Change.ToString().ToLowerInvariant()}: {change.ScenarioKey} / {change.CheckKey}"
            );
        }
        foreach (var mismatch in plan.Mismatches)
        {
            text.Append("\n  - ").Append(mismatch);
        }
        foreach (var skipped in plan.SkippedScenarios)
        {
            text.Append('\n').Append(Skipped(skipped));
        }
        return text.ToString();
    }

    public static string Skipped(SkippedScenario skipped) => $"skipped scenario '{skipped.Key}': {skipped.Reason}";
}
