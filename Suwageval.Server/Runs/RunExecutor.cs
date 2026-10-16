using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
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
    IOptions<RunOptions> options,
    ILogger<RunExecutor> logger
)
{
    private static readonly JsonSerializerOptions Json = ContractJson.Options;

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever breaks a run is recorded on it.")]
    public async Task ExecuteAsync(RunRow run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        try
        {
            var version = await agents.GetVersionAsync(run.AgentVersionId, cancellationToken);
            var declared = StoredContent.Version(version?.Content ?? throw new InvalidOperationException("The run's version is gone."));
            var plan = await PlanAsync(run, cancellationToken);
            var work = plan.SelectMany(scenario =>
                    Enumerable.Range(1, Math.Max(1, scenario.Content.Samples)).Select(sample => (scenario, sample))
                )
                .ToList();
            await runs.SetTotalAsync(run.Id, work.Count, cancellationToken);

            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await Parallel.ForEachAsync(
                    work,
                    new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxParallelScenarios, CancellationToken = stop.Token },
                    async (item, _) =>
                    {
                        // The agent is called with the outer token: stopping lets samples in flight finish, it only starts no new one.
                        if (await runs.IsCancelRequestedAsync(run.Id, cancellationToken))
                        {
                            await stop.CancelAsync();
                            return;
                        }
                        await RunSampleAsync(run, declared, item.scenario, item.sample, cancellationToken);
                    }
                );
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Cancelled by request: the status is set below.
            }
            var cancelled = stop.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            await CheckContractUnchangedAsync(run, version.ContentHash, cancellationToken);
            await runs.FinishAsync(run.Id, cancelled ? RunStatus.Cancelled : RunStatus.Done, cancellationToken: CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            LogRunFailed(error, run.Id);
            await runs.FinishAsync(run.Id, RunStatus.Failed, error.Message, CancellationToken.None);
        }
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

    private async Task<IReadOnlyList<PlannedScenario>> PlanAsync(RunRow run, CancellationToken cancellationToken)
    {
        var picked = await scenarios.ListAsync(run.AgentId, run.Tag, cancellationToken);
        var checksByScenario = (await checks.ListAsync(run.AgentId, cancellationToken: cancellationToken)).ToLookup(check =>
            check.ScenarioKey
        );
        return
        [
            .. picked.Select(scenario => new PlannedScenario(
                scenario,
                StoredContent.Scenario(scenario.Content),
                [.. checksByScenario[scenario.Key].Select(check => (check, StoredContent.Check(check)))]
            )),
        ];
    }

    private async Task RunSampleAsync(
        RunRow run,
        AgentContract declared,
        PlannedScenario scenario,
        int sample,
        CancellationToken cancellationToken
    )
    {
        var turn =
            scenario.Content.Continuation.Turns > 1
                ? TurnResult.Failed(
                    $"continuation.turns is {scenario.Content.Continuation.Turns}: more than one turn is not supported yet (multi-turn simulation comes after the MVP)."
                )
                : await agent.TurnAsync(new Uri(run.EndpointUrl), scenario.Content.Conversation, cancellationToken);

        NewSample stored;
        if (turn.Items is { } items)
        {
            var trace = new Trace([items]);
            var results = new List<NewCheckResult>();
            foreach (var (row, input) in scenario.Checks)
            {
                var verdict = input is JudgeCheck judgeCheck
                    ? await judge.JudgeAsync(judgeCheck, scenario.Content.Conversation, trace, cancellationToken)
                    : CheckEvaluator.Evaluate(input, trace);
                results.Add(
                    new NewCheckResult(
                        row.Id,
                        row.Revision,
                        verdict.Passed,
                        JsonSerializer.Serialize(verdict.Evidence, Json),
                        verdict.Message,
                        verdict.NotJudged
                    )
                );
            }
            stored = new NewSample(
                scenario.Row.Id,
                scenario.Row.Revision,
                sample,
                SampleStatus.Done,
                JsonSerializer.Serialize(items, Json),
                null,
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
                [],
                [
                    .. scenario.Checks.Select(check => new NewCheckResult(
                        check.Row.Id,
                        check.Row.Revision,
                        false,
                        "[]",
                        $"Not evaluated: {reason}"
                    )),
                ]
            );
        }
        await runs.AddSampleAsync(run.Id, stored, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} failed")]
    private partial void LogRunFailed(Exception error, Guid runId);

    private sealed record PlannedScenario(ScenarioRow Row, ScenarioContent Content, IReadOnlyList<(CheckRow Row, CheckInput Input)> Checks);
}
