using System.Text.Json;
using Microsoft.Extensions.Options;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Invocation;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Judges;

namespace Suwageval.Server.Evaluation.Runs;

/// <summary>
/// Carries out one claimed run (a gate run its own way, <see cref="GatePlan"/>): for every scenario picked by the run's tags (or all of them) and every sample of it, it calls
/// the agent under test with the scenario's conversation, stores what the agent produced, evaluates the scenario's checks on
/// it and stores the verdicts. Samples run in parallel up to <see cref="RunOptions.MaxParallelScenarios"/>.
/// </summary>
/// <remarks>
/// An agent that fails to answer is a result of that sample, never a failure of the run: its checks count as failed.
/// The run itself fails only when the server cannot do its part (the database, unreadable stored content).
/// A simulated scenario's sample is a whole simulated conversation (<see cref="UserSimulator"/>); a user who cannot be played is a result
/// of that sample too ("not simulated"), never a failure of the run.
/// </remarks>
internal sealed partial class RunExecutor(
    RunStore runs,
    ScenarioStore scenarios,
    CheckStore checks,
    AgentStore agents,
    AgentClient agent,
    UserSimulator simulator,
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
        if (run.GateRetryLimit is { } agentRetryLimit)
        {
            return RunLoop.ExecuteAsync(
                runs,
                options.Value.MaxParallelScenarios,
                run,
                async () =>
                {
                    var (declared, versionHash) = await DeclaredAsync(run, cancellationToken);
                    var plan = await PlanAsync(run, null, cancellationToken);
                    return GatePlan(run, declared, versionHash, plan, agentRetryLimit, cancellationToken);
                },
                error => LogRunFailed(error, run.Id),
                cancellationToken
            );
        }
        return RunLoop.ExecuteAsync(
            runs,
            options.Value.MaxParallelScenarios,
            run,
            async () =>
            {
                var (declared, versionHash) = await DeclaredAsync(run, cancellationToken);
                var agentSamples = (await agents.GetAsync(run.AgentId.ToString(), cancellationToken))?.Samples;
                var plan = await PlanAsync(run, agentSamples, cancellationToken);
                var work = plan.SelectMany(scenario => Enumerable.Range(1, scenario.Calls).Select(sample => (scenario, sample))).ToList();
                return new RunLoop.Plan<(PlannedScenario scenario, int sample)>(
                    work,
                    item =>
                        RunSampleAsync(
                            run,
                            declared,
                            item.scenario,
                            item.sample,
                            SamplePlan.JudgedOn(item.sample, item.scenario.Content, item.scenario.Checks, agentSamples),
                            cancellationToken
                        ),
                    () => CheckContractUnchangedAsync(run, versionHash, cancellationToken)
                );
            },
            error => LogRunFailed(error, run.Id),
            cancellationToken
        );
    }

    /// <summary>What the run's version declares, and its hash.</summary>
    private async Task<(AgentContract Declared, string Hash)> DeclaredAsync(RunRow run, CancellationToken cancellationToken)
    {
        var version = await agents.GetVersionAsync(run.AgentVersionId, cancellationToken);
        return (
            ContractContent.Read(version?.Content ?? throw new InvalidOperationException("The run's version is gone.")),
            version.ContentHash
        );
    }

    /// <summary>
    /// A gate run: one item per scenario with an <c>acGuardrail</c>, in key order. A scenario's attempts go one after another, each judged
    /// by every check of the scenario, until one passes them all or its limit is reached. The first scenario that reaches its limit stops
    /// the gate: the scenarios not started by then start no attempt, and are recorded as skipped once the items are done. The run is
    /// Done either way; its results say whether the gate passed.
    /// </summary>
    private RunLoop.Plan<PlannedScenario> GatePlan(
        RunRow run,
        AgentContract declared,
        string versionHash,
        IReadOnlyList<PlannedScenario> planned,
        int agentRetryLimit,
        CancellationToken cancellationToken
    )
    {
        var guardrails = planned
            .Where(scenario => scenario.Content.AcGuardrail is not null)
            .OrderBy(scenario => scenario.Row.Key, StringComparer.Ordinal)
            .ToList();
        var stoppedBy = (string?)null;
        var notStarted = new System.Collections.Concurrent.ConcurrentBag<string>();
        int Limit(PlannedScenario scenario)
        {
            return AcGuardrailRules.EffectiveRetryLimit(scenario.Content.AcGuardrail!.RetryLimit, agentRetryLimit);
        }

        async Task AttemptAsync(PlannedScenario scenario)
        {
            var limit = Limit(scenario);
            for (var attempt = 1; attempt <= limit; attempt++)
            {
                if (Volatile.Read(ref stoppedBy) is not null)
                {
                    if (attempt == 1)
                    {
                        notStarted.Add(scenario.Row.Key);
                    }
                    return;
                }
                if (await RunSampleAsync(run, declared, scenario, attempt, scenario.Checks, cancellationToken))
                {
                    return;
                }
            }
            Interlocked.CompareExchange(ref stoppedBy, scenario.Content.AcGuardrail!.Id, null);
        }

        return new RunLoop.Plan<PlannedScenario>(
            guardrails,
            AttemptAsync,
            async () =>
            {
                if (Volatile.Read(ref stoppedBy) is { } failed && !notStarted.IsEmpty)
                {
                    await runs.SetSkippedAsync(
                        run.Id,
                        [
                            .. notStarted
                                .Order(StringComparer.Ordinal)
                                .Select(key => new SkippedScenario { Key = key, Reason = $"Not run: the gate stopped at {failed}." }),
                        ],
                        cancellationToken
                    );
                }
                await CheckContractUnchangedAsync(run, versionHash, cancellationToken);
            },
            guardrails.Sum(Limit)
        );
    }

    /// <summary>
    /// Reads the agent's contract again: a different one means the agent was redeployed while the run was going, so some
    /// samples may not be of the version the run says. An agent that does not answer now proves nothing either way.
    /// </summary>
    private async Task CheckContractUnchangedAsync(RunRow run, string versionHash, CancellationToken cancellationToken)
    {
        var now = await agent.GetContractAsync(new Uri(run.EndpointUrl), cancellationToken);
        if (now.Contract is { } contract && AgentContractHash.Of(contract) != versionHash)
        {
            await runs.MarkContractChangedAsync(run.Id, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<PlannedScenario>> PlanAsync(RunRow run, int? agentSamples, CancellationToken cancellationToken)
    {
        var picked = await scenarios.ListAsync(run.AgentId, run.Tags, cancellationToken);
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

    /// <summary>Plays one sample of <paramref name="scenario"/>, judges it with <paramref name="judged"/> and stores it; true when the agent answered and every one of them passed.</summary>
    private async Task<bool> RunSampleAsync(
        RunRow run,
        AgentContract declared,
        PlannedScenario scenario,
        int sample,
        IEnumerable<(CheckRow Row, CheckInput Input)> judged,
        CancellationToken cancellationToken
    )
    {
        var played = scenario.Content.Continuation.SimulatedUser is { } simulatedUser
            ? await SimulateAsync(run, scenario, sample, simulatedUser, cancellationToken)
            : await AnswerOnceAsync(run, scenario, sample, cancellationToken);

        var results = new List<NewCheckResult>();
        if (played is { Status: SampleStatus.Done, Output: { } output })
        {
            var trace = scenario.Content.TraceOf(output);
            foreach (var (row, input) in judged)
            {
                results.Add(
                    await CheckEvaluation.EvaluateAsync(judge, argumentJudge, row, input, scenario.Content, trace, cancellationToken)
                );
            }
        }
        else
        {
            var reason = played.Error ?? "The agent produced nothing.";
            results.AddRange(judged.Select(check => CheckEvaluation.NotEvaluated(check.Row, reason)));
        }

        await runs.AddSampleAsync(
            run.Id,
            new NewSample(
                scenario.Row.Id,
                scenario.Row.Revision,
                sample,
                played.Status,
                played.Output is null ? null : JsonSerializer.Serialize(played.Output, Json),
                played.Error,
                played.DurationMs,
                played.Output is null ? [] : UndeclaredOutput.Of(played.Output, declared),
                results,
                played.TurnDurationsMs,
                played.EndedByUser
            ),
            cancellationToken
        );
        return played.Status == SampleStatus.Done && results.Count > 0 && results.TrueForAll(result => result.Passed);
    }

    /// <summary>A scenario of one turn: the agent answers the conversation's last message.</summary>
    private async Task<Played> AnswerOnceAsync(RunRow run, PlannedScenario scenario, int sample, CancellationToken cancellationToken)
    {
        if (scenario.Content.Continuation.Turns > 1)
        {
            // Stored before more turns needed a simulated user; validation refuses such a scenario now.
            return new Played(
                SampleStatus.Failed,
                null,
                $"continuation.turns is {scenario.Content.Continuation.Turns} without continuation.simulatedUser: more than one turn needs a simulated user.",
                null
            );
        }
        var turn = await agent.TurnAsync(
            new Uri(run.EndpointUrl),
            scenario.Content.Conversation,
            new TurnContext(
                run.Id,
                scenario.Row.Key,
                sample,
                ScenarioType.Turn,
                scenario.Row.Tags,
                run.Tags,
                run.RunGroup,
                Guid.NewGuid().ToString(),
                scenario.Content.Fixtures,
                run.AgentVersionId
            ),
            cancellationToken
        );
        return turn.Items is { } items
            ? new Played(SampleStatus.Done, items, null, turn.DurationMs)
            : new Played(SampleStatus.Failed, null, turn.Error ?? "The agent produced nothing.", turn.DurationMs);
    }

    /// <summary>
    /// A simulation: turn after turn, each ending with the agent's answer, up to the scenario's turns. The simulated user writes the
    /// message that opens a turn (every turn but the first when the conversation ends with a user message, which the agent answers
    /// first) and may end the conversation instead. Every turn is sent the whole log so far, under one conversation id. An agent that
    /// does not answer, or a user who cannot be played, stops the sample and keeps what happened before.
    /// </summary>
    private async Task<Played> SimulateAsync(
        RunRow run,
        PlannedScenario scenario,
        int sample,
        string simulatedUser,
        CancellationToken cancellationToken
    )
    {
        var content = scenario.Content;
        var conversation = new List<ConversationItem>(content.Conversation);
        var output = new List<ConversationItem>();
        var durations = new List<int>();
        var context = new TurnContext(
            run.Id,
            scenario.Row.Key,
            sample,
            ScenarioType.Simulation,
            scenario.Row.Tags,
            run.Tags,
            run.RunGroup,
            Guid.NewGuid().ToString(),
            content.Fixtures,
            run.AgentVersionId
        );
        var agentStarts = content.Conversation is [.., UserMessage];
        for (var turn = 1; turn <= content.Continuation.Turns; turn++)
        {
            if (turn > 1 || !agentStarts)
            {
                var next = await simulator.NextAsync(simulatedUser, conversation, cancellationToken);
                if (next.Failure is { } failure)
                {
                    return Played.Of(SampleStatus.NotSimulated, output, $"Not simulated: {failure}.", durations);
                }
                if (next.Message is not { } message)
                {
                    return Played.Of(SampleStatus.Done, output, null, durations, endedByUser: true);
                }
                var said = new UserMessage { Text = message };
                conversation.Add(said);
                output.Add(said);
            }

            var answer = await agent.TurnAsync(new Uri(run.EndpointUrl), conversation, context, cancellationToken);
            if (answer.DurationMs is { } duration)
            {
                durations.Add(duration);
            }
            if (answer.Items is not { } items)
            {
                return Played.Of(SampleStatus.Failed, output, $"Turn {turn}: {answer.Error ?? "The agent produced nothing."}", durations);
            }
            conversation.AddRange(items);
            output.AddRange(items);
        }
        return Played.Of(SampleStatus.Done, output, null, durations);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} failed")]
    private partial void LogRunFailed(Exception error, Guid runId);

    /// <summary>What talking to the agent came to for one sample, before any check looked at it.</summary>
    private sealed record Played(
        SampleStatus Status,
        IReadOnlyList<ConversationItem>? Output,
        string? Error,
        int? DurationMs,
        IReadOnlyList<int>? TurnDurationsMs = null,
        bool EndedByUser = false
    )
    {
        /// <summary>A simulation's outcome: its time is the sum of the agent's turns, each of them kept.</summary>
        public static Played Of(
            SampleStatus status,
            IReadOnlyList<ConversationItem> output,
            string? error,
            List<int> durations,
            bool endedByUser = false
        ) => new(status, output, error, durations.Count == 0 ? null : durations.Sum(), durations, endedByUser);
    }

    private sealed record PlannedScenario(
        ScenarioRow Row,
        ScenarioContent Content,
        IReadOnlyList<(CheckRow Row, CheckInput Input)> Checks,
        int Calls
    );
}
