using System.Text.Json;
using Microsoft.Extensions.Options;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Judges;

namespace Suwageval.Server.Evaluation.Runs;

internal sealed partial class RunRechecker(
    RunStore runs,
    ScenarioStore scenarios,
    CheckStore checks,
    AgentStore agents,
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
                // Why: unchanged verdicts come from the run the person looked at, because an earlier re-check has the same answers already judged
                var verdictsId = run.VerdictsFrom ?? sourceId;
                var verdictSamples =
                    verdictsId == sourceId ? sourceSamples : await runs.ListScenarioResultsAsync(verdictsId, cancellationToken);
                var verdictSampleOf = verdictSamples.ToDictionary(sample => (sample.ScenarioId, sample.Sample), sample => sample.Id);
                var verdictsOf = (await runs.ListCheckResultsAsync(verdictsId, cancellationToken)).ToLookup(verdict =>
                    verdict.ScenarioResultId
                );
                var agentSamples = (await agents.GetAsync(run.AgentId.ToString(), cancellationToken))?.Samples;
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
                            agentSamples,
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
        int? agentSamples,
        IReadOnlyList<CheckResultRow> sourceVerdicts,
        CancellationToken cancellationToken
    )
    {
        var results = new List<NewCheckResult>();
        if (sample.Status == nameof(SampleStatus.Done) && sample.Output is { } output)
        {
            var trace = scenario.Content.TraceOf(JsonSerializer.Deserialize<IReadOnlyList<ConversationItem>>(output, Json) ?? []);
            foreach (var (row, input) in SamplePlan.JudgedOn(sample.Sample, scenario.Content, scenario.Checks, agentSamples))
            {
                var same = sourceVerdicts.FirstOrDefault(verdict => verdict.CheckId == row.Id && verdict.CheckRevision == row.Revision);
                results.Add(
                    same is null
                        ? await CheckEvaluation.EvaluateAsync(judge, argumentJudge, row, input, scenario.Content, trace, cancellationToken)
                        : new NewCheckResult(
                            row.Id,
                            row.Revision,
                            same.Passed,
                            same.Evidence,
                            same.Message,
                            same.NotJudged,
                            same.JudgeModel
                        )
                );
            }
        }
        else
        {
            var reason = sample.Error ?? "The agent produced nothing.";
            results.AddRange(
                SamplePlan
                    .JudgedOn(sample.Sample, scenario.Content, scenario.Checks, agentSamples)
                    .Select(check => CheckEvaluation.NotEvaluated(check.Row, reason))
            );
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
                results,
                sample.TurnDurationsMs,
                sample.EndedByUser
            ),
            cancellationToken
        );
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Re-check {RunId} failed")]
    private partial void LogRecheckFailed(Exception error, Guid runId);

    private sealed record Rechecked(ScenarioRow Row, ScenarioContent Content, IReadOnlyList<(CheckRow Row, CheckInput Input)> Checks);
}
