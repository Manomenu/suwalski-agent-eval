using System.Text.Json;
using Microsoft.Extensions.Options;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Invocation;
using Suwageval.Server.Judges;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>
/// Carries out one claimed run: for every scenario picked by the run's tag (or all of them) and every sample of it, it calls
/// the agent under test with the scenario's conversation, stores what the agent produced, evaluates the scenario's checks on
/// it and stores the verdicts. Samples run in parallel up to <see cref="RunOptions.MaxParallelScenarios"/>.
/// </summary>
/// <remarks>
/// An agent that fails to answer is a result of that sample, never a failure of the run: its checks count as failed.
/// The run itself fails only when the server cannot do its part (the database, unreadable stored content).
/// Every sample is one turn for now; a scenario asking for more turns (multi-turn simulation comes after the MVP) fails the same way.
/// </remarks>
internal sealed partial class RunExecutor(
    RunStore runs,
    ScenarioStore scenarios,
    CheckStore checks,
    AgentStore agents,
    AgentClient agent,
    Judge judge,
    ArgumentJudge argumentJudge,
    IOptions<RunOptions> options,
    ILogger<RunExecutor> logger
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
                var version = await agents.GetVersionAsync(run.AgentVersionId, cancellationToken);
                var declared = ContractContent.Read(version?.Content ?? throw new InvalidOperationException("The run's version is gone."));
                var agentSamples = (await agents.GetAsync(run.AgentId.ToString(), cancellationToken))?.Samples;
                var plan = await PlanAsync(run, agentSamples, cancellationToken);
                var work = plan.SelectMany(scenario => Enumerable.Range(1, scenario.Calls).Select(sample => (scenario, sample))).ToList();
                return new RunLoop.Plan<(PlannedScenario scenario, int sample)>(
                    work,
                    item => RunSampleAsync(run, declared, item.scenario, item.sample, agentSamples, cancellationToken),
                    () => CheckContractUnchangedAsync(run, version.ContentHash, cancellationToken)
                );
            },
            error => LogRunFailed(error, run.Id),
            cancellationToken
        );
    }

    /// <summary>
    /// Reads the agent's contract again: a different one means the agent was redeployed while the run was going, so some
    /// samples may not be of the version the run says. An agent that does not answer now proves nothing either way.
    /// </summary>
    private async Task CheckContractUnchangedAsync(RunRow run, string versionHash, CancellationToken cancellationToken)
    {
        var now = await agent.GetContractAsync(new Uri(run.EndpointUrl), cancellationToken);
        if (now.Contract is { } contract && ContractContent.Hash(contract) != versionHash)
        {
            await runs.MarkContractChangedAsync(run.Id, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<PlannedScenario>> PlanAsync(RunRow run, int? agentSamples, CancellationToken cancellationToken)
    {
        var picked = await scenarios.ListAsync(run.AgentId, run.Tag, cancellationToken);
        var checksByScenario = (await checks.ListAsync(run.AgentId, cancellationToken: cancellationToken)).ToLookup(check =>
            check.ScenarioKey
        );
        return
        [
            .. picked.Select(scenario =>
            {
                var content = StoredContent.Scenario(scenario.Content);
                var scenarioChecks = checksByScenario[scenario.Key].Select(check => (check, StoredContent.Check(check))).ToList();
                return new PlannedScenario(scenario, content, scenarioChecks, SamplePlan.Calls(content, scenarioChecks, agentSamples));
            }),
        ];
    }

    private async Task RunSampleAsync(
        RunRow run,
        AgentContract declared,
        PlannedScenario scenario,
        int sample,
        int? agentSamples,
        CancellationToken cancellationToken
    )
    {
        var turn =
            scenario.Content.Continuation.Turns > 1
                ? TurnResult.Failed(
                    $"continuation.turns is {scenario.Content.Continuation.Turns}: more than one turn is not supported yet (multi-turn simulation comes after the MVP)."
                )
                : await agent.TurnAsync(
                    new Uri(run.EndpointUrl),
                    scenario.Content.Conversation,
                    new TurnContext(run.Id, scenario.Row.Key, sample, scenario.Content.Fixtures),
                    cancellationToken
                );

        NewSample stored;
        if (turn.Items is { } items)
        {
            var trace = new Trace([items]);
            var results = new List<NewCheckResult>();
            foreach (var (row, input) in SamplePlan.JudgedOn(sample, scenario.Content, scenario.Checks, agentSamples))
            {
                results.Add(
                    await CheckEvaluation.EvaluateAsync(judge, argumentJudge, row, input, scenario.Content, trace, cancellationToken)
                );
            }
            stored = new NewSample(
                scenario.Row.Id,
                scenario.Row.Revision,
                sample,
                SampleStatus.Done,
                JsonSerializer.Serialize(items, Json),
                null,
                turn.DurationMs,
                UndeclaredOutput.Of(items, declared),
                results
            );
        }
        else
        {
            var reason = turn.Error ?? "The agent produced nothing.";
            stored = new NewSample(
                scenario.Row.Id,
                scenario.Row.Revision,
                sample,
                SampleStatus.Failed,
                null,
                reason,
                turn.DurationMs,
                [],
                [
                    .. SamplePlan
                        .JudgedOn(sample, scenario.Content, scenario.Checks, agentSamples)
                        .Select(check => CheckEvaluation.NotEvaluated(check.Row, reason)),
                ]
            );
        }
        await runs.AddSampleAsync(run.Id, stored, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} failed")]
    private partial void LogRunFailed(Exception error, Guid runId);

    private sealed record PlannedScenario(
        ScenarioRow Row,
        ScenarioContent Content,
        IReadOnlyList<(CheckRow Row, CheckInput Input)> Checks,
        int Calls
    );
}
