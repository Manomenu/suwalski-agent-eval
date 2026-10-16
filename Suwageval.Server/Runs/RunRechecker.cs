using System.Diagnostics.CodeAnalysis;
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

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever breaks a run is recorded on it.")]
    public async Task ExecuteAsync(RunRow run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        try
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
            var kept = plan.Kept.ToDictionary(scenario => scenario.Id);
            var work = sourceSamples.Where(sample => kept.ContainsKey(sample.ScenarioId)).ToList();
            await runs.SetSkippedAsync(run.Id, plan.Skipped, cancellationToken);
            await runs.SetTotalAsync(run.Id, work.Count, cancellationToken);

            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await Parallel.ForEachAsync(
                    work,
                    new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxParallelScenarios, CancellationToken = stop.Token },
                    async (sample, _) =>
                    {
                        if (await runs.IsCancelRequestedAsync(run.Id, cancellationToken))
                        {
                            await stop.CancelAsync();
                            return;
                        }
                        var scenario = kept[sample.ScenarioId];
                        var current = checksByScenario[scenario.Key].Select(row => (row, StoredContent.Check(row))).ToList();
                        await RecheckSampleAsync(
                            run.Id,
                            scenario,
                            sample,
                            [
                                .. verdictSampleOf.TryGetValue((sample.ScenarioId, sample.Sample), out var verdictSample)
                                    ? verdictsOf[verdictSample]
                                    : [],
                            ],
                            current,
                            cancellationToken
                        );
                    }
                );
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Cancelled by request: the status is set below.
            }
            var cancelled = stop.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            await runs.FinishAsync(run.Id, cancelled ? RunStatus.Cancelled : RunStatus.Done, cancellationToken: CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            LogRecheckFailed(error, run.Id);
            await runs.FinishAsync(run.Id, RunStatus.Failed, error.Message, CancellationToken.None);
        }
    }

    private async Task RecheckSampleAsync(
        Guid runId,
        ScenarioRow scenario,
        ScenarioResultRow sample,
        IReadOnlyList<CheckResultRow> sourceVerdicts,
        IReadOnlyList<(CheckRow Row, CheckInput Input)> current,
        CancellationToken cancellationToken
    )
    {
        var content = StoredContent.Scenario(scenario.Content);
        var results = new List<NewCheckResult>();
        if (sample.Output is { } output)
        {
            var trace = new Trace([JsonSerializer.Deserialize<IReadOnlyList<ConversationItem>>(output, Json) ?? []]);
            foreach (var (row, input) in current)
            {
                var same = sourceVerdicts.FirstOrDefault(verdict => verdict.CheckId == row.Id && verdict.CheckRevision == row.Revision);
                results.Add(
                    same is null
                        ? await CheckEvaluation.EvaluateAsync(judge, argumentJudge, row, input, content, trace, cancellationToken)
                        : new NewCheckResult(row.Id, row.Revision, same.Passed, same.Evidence, same.Message, same.NotJudged)
                );
            }
        }
        else
        {
            var reason = sample.Error ?? "The agent produced nothing.";
            results.AddRange(current.Select(check => CheckEvaluation.NotEvaluated(check.Row, reason)));
        }
        await runs.AddSampleAsync(
            runId,
            new NewSample(
                scenario.Id,
                scenario.Revision,
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
}
