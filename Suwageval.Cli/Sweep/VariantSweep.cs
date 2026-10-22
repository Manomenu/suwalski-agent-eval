using System.Diagnostics;
using System.Globalization;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Sweep;

/// <summary>Runs a command line of agent.yaml's <c>sweep:</c> section, its output going to <paramref name="output"/>; the exit code.</summary>
internal delegate Task<int> ShellCommand(string commandLine, TextWriter output, CancellationToken cancellationToken);

/// <summary>What happened to one variant: its run (if one was started) and one line about it.</summary>
internal sealed record VariantOutcome(string Variant, Guid? RunId, string Summary, bool Failed);

/// <param name="Outcomes">One per variant that was attempted, in order.</param>
/// <param name="Interrupted">Ctrl+C stopped the sweep: the variants after the interrupted one were not run.</param>
/// <param name="RestoreFailed">The <c>restore</c> command failed: the agent may still serve the last variant.</param>
internal sealed record SweepResult(IReadOnlyList<VariantOutcome> Outcomes, bool Interrupted, bool RestoreFailed)
{
    public bool Failed => Interrupted || RestoreFailed || Outcomes.Any(outcome => outcome.Failed);
}

/// <summary>
/// Runs the agent's scenarios once per variant: reconfigures the agent, waits until its contract's label is the variant's name, runs.
/// A variant that could not be run is a row of the result and the sweep goes on; <c>after</c> follows every variant that was attempted and
/// <c>restore</c> the end of the sweep, also after Ctrl+C.
/// </summary>
/// <param name="settings">The commands to run.</param>
/// <param name="shell">Runs the command lines.</param>
/// <param name="readLabel">The label of the contract the agent serves now; null when it cannot be read.</param>
/// <param name="runScenarios">Runs the scenarios on the server and waits for the end; reports on the run instead of throwing for what the server answers.</param>
/// <param name="output">Where the variants are announced and the commands' output goes.</param>
/// <param name="labelWait">How long the label may take to show the variant's name.</param>
/// <param name="pollInterval">How often the label is read while waiting.</param>
internal sealed class VariantSweep(
    SweepSettings settings,
    ShellCommand shell,
    Func<CancellationToken, Task<string?>> readLabel,
    Func<CancellationToken, Task<VariantOutcome>> runScenarios,
    TextWriter output,
    TimeSpan labelWait,
    TimeSpan pollInterval
)
{
    public const string VariantPlaceholder = "{variant}";
    public const string GroupPlaceholder = "{group}";

    public static string Fill(string commandLine, string variant, string group) =>
        commandLine
            .Replace(VariantPlaceholder, variant, StringComparison.Ordinal)
            .Replace(GroupPlaceholder, group, StringComparison.Ordinal);

    public async Task<SweepResult> RunAsync(IReadOnlyList<string> variants, string group, CancellationToken cancellationToken)
    {
        var outcomes = new List<VariantOutcome>();
        var backendTouched = false;
        var interrupted = false;
        var restoreFailed = false;
        try
        {
            foreach (var variant in variants)
            {
                await output.WriteLineAsync($">>> {variant}");
                VariantOutcome outcome;
                try
                {
                    var (ran, touched) = await TryAsync(variant, group, cancellationToken);
                    outcome = ran;
                    backendTouched |= touched;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    outcome = new VariantOutcome(variant, null, "interrupted", true);
                    interrupted = true;
                    backendTouched = true;
                }
                if (settings.After is { } after)
                {
                    var exit = await shell(Fill(after, variant, group), output, CancellationToken.None);
                    if (exit != 0)
                    {
                        outcome = outcome with { Summary = $"{outcome.Summary}; after exited {exit}", Failed = true };
                    }
                }
                outcomes.Add(outcome);
                if (interrupted)
                {
                    break;
                }
            }
        }
        finally
        {
            if (backendTouched && settings.Restore is { } restore)
            {
                await output.WriteLineAsync(">>> restore");
                restoreFailed = await shell(Fill(restore, "", group), output, CancellationToken.None) != 0;
            }
        }
        return new SweepResult(outcomes, interrupted, restoreFailed);
    }

    /// <returns>The outcome and whether the agent was reconfigured (so <c>restore</c> is due).</returns>
    private async Task<(VariantOutcome Outcome, bool Touched)> TryAsync(string variant, string group, CancellationToken cancellationToken)
    {
        if (settings.Before is { } before)
        {
            var exit = await shell(Fill(before, variant, group), output, cancellationToken);
            if (exit != 0)
            {
                return (Failure(variant, $"before exited {exit}"), false);
            }
        }
        var reconfigured = await shell(Fill(settings.Reconfigure, variant, group), output, cancellationToken);
        if (reconfigured != 0)
        {
            return (Failure(variant, $"reconfigure exited {reconfigured}"), true);
        }
        if (await LabelProblemAsync(variant, cancellationToken) is { } problem)
        {
            return (Failure(variant, problem), true);
        }
        return (await runScenarios(cancellationToken) with { Variant = variant }, true);
    }

    private async Task<string?> LabelProblemAsync(string variant, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var label = await readLabel(cancellationToken);
            if (label == variant)
            {
                return null;
            }
            if (clock.Elapsed >= labelWait)
            {
                var waited = labelWait.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
                return label is null
                    ? $"the agent's contract could not be read within {waited} s"
                    : $"the contract's label is '{label}', not '{variant}', after {waited} s";
            }
            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    private static VariantOutcome Failure(string variant, string summary) => new(variant, null, summary, true);
}
