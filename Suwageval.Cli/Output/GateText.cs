using System.Globalization;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Output;

/// <summary>
/// What <c>suwageval gate</c> prints for a finished gate run, and the exit code it gives. Quiet: a gate that passed is one line; a gate
/// that failed says, for each acceptance criterion that failed, every attempt's failed checks with the verdict's message and what the
/// agent did in the last attempt, so a CI log alone tells what broke.
/// </summary>
internal static class GateText
{
    private const string Indent = "  ";

    /// <summary>0 when the gate passed, 1 when an acceptance criterion used up its attempts, 2 when the run did not finish.</summary>
    public static int ExitCode(RunResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Run.Status != RunStatus.Done || results.Gate is null ? 2
            : results.Gate.Passed ? 0
            : 1;
    }

    public static IReadOnlyList<string> Lines(RunResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (results.Run.Status != RunStatus.Done || results.Gate is not { } gate)
        {
            return [RunVerdict.Line(results)];
        }
        var guarded = results.Scenarios.Where(scenario => scenario.AcGuardrail is not null).ToList();
        var passed = guarded.Count(scenario => scenario.AcGuardrail!.Passed == true);
        if (gate.Passed)
        {
            return [string.Create(CultureInfo.InvariantCulture, $"PASSED {passed} of {guarded.Count} acceptance criteria")];
        }

        var lines = new List<string>();
        foreach (var scenario in guarded.Where(scenario => scenario.AcGuardrail!.Passed == false))
        {
            lines.AddRange(Failure(scenario));
        }
        var stopped = guarded.Where(scenario => scenario.AcGuardrail!.Passed is null).Select(scenario => scenario.AcGuardrail!.Id).ToList();
        if (stopped.Count > 0)
        {
            lines.Add($"Stopped before their last attempt: {string.Join(", ", stopped)}");
        }
        if (results.Run.SkippedScenarios.Count > 0)
        {
            lines.Add($"Not run, the gate stopped first: {string.Join(", ", results.Run.SkippedScenarios.Select(skipped => skipped.Key))}");
        }
        lines.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"FAILED {gate.FailedGuardrail} ({passed} of {guarded.Count + results.Run.SkippedScenarios.Count} acceptance criteria passed); everything: suwageval results {results.Run.Id}"
            )
        );
        return lines;
    }

    /// <summary>A failed criterion: its line, each attempt's failed checks (or the agent's error), and what the agent did in the last one.</summary>
    private static IEnumerable<string> Failure(ScenarioRunResult scenario)
    {
        var guardrail = scenario.AcGuardrail!;
        yield return string.Create(
            CultureInfo.InvariantCulture,
            $"FAILED {guardrail.Id} (scenario {scenario.Key}): {guardrail.Attempts} of {guardrail.RetryLimit} attempts failed"
        );
        foreach (var attempt in scenario.Samples.OrderBy(sample => sample.Sample))
        {
            var prefix = string.Create(CultureInfo.InvariantCulture, $"{Indent}attempt {attempt.Sample}: ");
            if (attempt.Status != SampleStatus.Done)
            {
                yield return $"{prefix}the agent gave no answer: {attempt.Error ?? attempt.Status.ToString()}";
                continue;
            }
            foreach (var check in scenario.Checks)
            {
                if (check.Samples.FirstOrDefault(sample => sample.Sample == attempt.Sample) is { Passed: false } verdict)
                {
                    yield return $"{prefix}{check.Key}: {ConversationText.Multiline(verdict.Message)}";
                }
            }
        }
        if (scenario.Samples.MaxBy(sample => sample.Sample)?.Output is { Count: > 0 } output)
        {
            yield return $"{Indent}what the agent did in the last attempt:";
            foreach (var item in output)
            {
                // A model's answer often starts or ends with blank lines; in a log they only push the reason apart.
                var shown = item is AssistantMessage message ? message with { Text = message.Text.Trim() } : item;
                yield return $"{Indent}{Indent}{ConversationText.Item(shown)}";
            }
        }
    }
}
