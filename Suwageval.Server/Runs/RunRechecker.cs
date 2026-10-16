using System.Text.Json;
using Microsoft.Extensions.Options;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Checks;
using Suwageval.Server.Judges;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>
/// Carries out one claimed re-check: the stored samples of the source run (the answers of its agent) are copied into the re-check
/// and judged again by the scenario's current checks — the agent is not called. A check whose revision is the one that judged the
/// source gets that verdict copied (no new model call, no noise from a judge); a changed or new check is evaluated on the stored output
/// the way a run does. Scenarios that changed or were deleted since the source are skipped (<see cref="RecheckScope"/>).
/// </summary>
internal sealed partial class RunRechecker(
    RunStore runs,
    ScenarioStore scenarios,
    CheckStore checks,
    Judge judge,
    ArgumentJudge argumentJudge,
    IOptions<RunOptions> options,
    ILogger<RunRechecker> logger
)
{
    private static readonly JsonSerializerOptions Json = ContractJson.Options;

    public Task ExecuteAsync(RunRow run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        return RunLoop.ExecuteAsync(
            runs,
            options.Value.MaxParallelScenarios,
            run,
            async () =>
            {
                var sourceId = run.RecheckOf ?? throw new InvalidOperationException("The run is not a re-check.");
                var sourceSamples = await runs.ListScenarioResultsAsync(sourceId, cancellationToken);
                // Unchanged verdicts come from the run the person looked at (an earlier re-check has the same answers, already judged).
                var verdictsId = run.VerdictsFrom ?? sourceId;
                var verdictSamples =
                    verdictsId == sourceId ? sourceSamples : await runs.ListScenarioResultsAsync(verdictsId, cancellationToken);
                var verdictSampleOf = verdictSamples.ToDictionary(sample => (sample.ScenarioId, sample.Sample), sample => sample.Id);
                var verdictsOf = (await runs.ListCheckResultsAsync(verdictsId, cancellationToken)).ToLookup(verdict =>
                    verdict.ScenarioResultId
                );
                var plan = RecheckScope.Of(sourceSamples, await scenarios.ListAsync(run.AgentId, cancellationToken: cancellationToken));
                var checksByScenario = (await checks.ListAsync(run.AgentId, cancellationToken: cancellationToken)).ToLookup(check =>
                    check.ScenarioKey
                );
                var kept = plan.Kept.ToDictionary(
                    scenario => scenario.Id,
                    scenario => new Rechecked(
                        scenario,
                        StoredContent.Scenario(scenario.Content),
                        [.. checksByScenario[scenario.Key].Select(row => (row, StoredContent.Check(row)))]
                    )
                );
                var work = sourceSamples.Where(sample => kept.ContainsKey(sample.ScenarioId)).ToList();
                await runs.SetSkippedAsync(run.Id, plan.Skipped, cancellationToken);
                return new RunLoop.Plan<ScenarioResultRow>(
                    work,
                    sample =>
                        RecheckSampleAsync(
                            run.Id,
                            kept[sample.ScenarioId],
                            sample,
                            [
                                .. verdictSampleOf.TryGetValue((sample.ScenarioId, sample.Sample), out var verdictSample)
                                    ? verdictsOf[verdictSample]
                                    : [],
                            ],
                            cancellationToken
                        )
                );
            },
            error => LogRecheckFailed(error, run.Id),
            cancellationToken
        );
    }

    private async Task RecheckSampleAsync(
        Guid runId,
        Rechecked scenario,
        ScenarioResultRow sample,
        IReadOnlyList<CheckResultRow> sourceVerdicts,
        CancellationToken cancellationToken
    )
    {
        var results = new List<NewCheckResult>();
        if (sample.Output is { } output)
        {
            var trace = new Trace([JsonSerializer.Deserialize<IReadOnlyList<ConversationItem>>(output, Json) ?? []]);
            foreach (var (row, input) in scenario.Checks)
            {
                var same = sourceVerdicts.FirstOrDefault(verdict => verdict.CheckId == row.Id && verdict.CheckRevision == row.Revision);
                results.Add(
                    same is null
                        ? await CheckEvaluation.EvaluateAsync(judge, argumentJudge, row, input, scenario.Content, trace, cancellationToken)
                        : new NewCheckResult(row.Id, row.Revision, same.Passed, same.Evidence, same.Message, same.NotJudged)
                );
            }
        }
        else
        {
            var reason = sample.Error ?? "The agent produced nothing.";
            results.AddRange(scenario.Checks.Select(check => CheckEvaluation.NotEvaluated(check.Row, reason)));
        }
        await runs.AddSampleAsync(
            runId,
            new NewSample(
                scenario.Row.Id,
                scenario.Row.Revision,
                sample.Sample,
                Enum.Parse<SampleStatus>(sample.Status),
                sample.Output,
                sample.Error,
                sample.DurationMs,
                JsonSerializer.Deserialize<IReadOnlyList<string>>(sample.Undeclared, Json) ?? [],
                results
            ),
            cancellationToken
        );
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Re-check {RunId} failed")]
    private partial void LogRecheckFailed(Exception error, Guid runId);

    private sealed record Rechecked(ScenarioRow Row, ScenarioContent Content, IReadOnlyList<(CheckRow Row, CheckInput Input)> Checks);
}
