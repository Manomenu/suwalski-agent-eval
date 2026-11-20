using System.Globalization;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Output;

internal static class GateText
{
    private const string Indent = "  ";

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
        var guarded = new List<(ScenarioRunResult Scenario, ScenarioGuardrailResult Guardrail)>();
        foreach (var scenario in results.Scenarios)
        {
            if (scenario.AcGuardrail is { } guardrail)
            {
                guarded.Add((scenario, guardrail));
            }
        }
        var passed = guarded.Count(item => item.Guardrail.Passed == true);
        if (gate.Passed)
        {
            return [string.Create(CultureInfo.InvariantCulture, $"PASSED {passed} of {guarded.Count} acceptance criteria")];
        }

        var lines = new List<string>();
        foreach (var (scenario, guardrail) in guarded.Where(item => item.Guardrail.Passed == false))
        {
            lines.AddRange(Failure(scenario, guardrail));
        }
        var stopped = guarded.Where(item => item.Guardrail.Passed is null).Select(item => item.Guardrail.Id).ToList();
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

    private static IEnumerable<string> Failure(ScenarioRunResult scenario, ScenarioGuardrailResult guardrail)
    {
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
                var shown = item is AssistantMessage message ? message with { Text = message.Text.Trim() } : item;
                yield return $"{Indent}{Indent}{ConversationText.Item(shown)}";
            }
        }
    }
}
