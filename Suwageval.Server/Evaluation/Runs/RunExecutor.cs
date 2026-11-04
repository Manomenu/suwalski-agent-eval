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

    private async Task<(AgentContract Declared, string Hash)> DeclaredAsync(RunRow run, CancellationToken cancellationToken)
    {
        var version = await agents.GetVersionAsync(run.AgentVersionId, cancellationToken);
        return (
            ContractContent.Read(version?.Content ?? throw new InvalidOperationException("The run's version is gone.")),
            version.ContentHash
        );
    }

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
            // Why: gate items are only scenarios with an acGuardrail
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
            // Why: gate items are only scenarios with an acGuardrail
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

    private async Task<Played> AnswerOnceAsync(RunRow run, PlannedScenario scenario, int sample, CancellationToken cancellationToken)
    {
        if (scenario.Content.Continuation.Turns > 1)
        {
            // Why: stored before more turns needed a simulated user, and validation refuses such a scenario now
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

    private sealed record Played(
        SampleStatus Status,
        IReadOnlyList<ConversationItem>? Output,
        string? Error,
        int? DurationMs,
        IReadOnlyList<int>? TurnDurationsMs = null,
        bool EndedByUser = false
    )
    {
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
