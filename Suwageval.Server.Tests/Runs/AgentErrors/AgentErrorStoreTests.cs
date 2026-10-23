using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;
using Suwageval.Server.Runs.AgentErrors;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Tests.Runs.AgentErrors;

public sealed class AgentErrorStoreTests(TestDatabase database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private RunStore Store() => new(database.Pool);

    private static Task<Guid> EnqueueAsync(RunStore store, SeededAgent agent) =>
        store.EnqueueAsync(agent.Id, agent.VersionId, [], agent.EndpointUrl, cancellationToken: Cancellation);

    [Fact]
    public async Task Agent_errors_count_samples_without_an_answer_per_scenario_with_the_newest_error_worst_first()
    {
        var store = Store();
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund"), SeededAgent.Scenario("chat"), SeededAgent.Scenario("fine")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );
        var scenarios = new ScenarioStore(database.Pool);
        var refund = (await scenarios.GetAsync(agent.Id, "refund", Cancellation))!;
        var chat = (await scenarios.GetAsync(agent.Id, "chat", Cancellation))!;
        var fine = (await scenarios.GetAsync(agent.Id, "fine", Cancellation))!;
        var first = await EnqueueAsync(store, agent);
        var second = await EnqueueAsync(store, agent);
        var outside = await EnqueueAsync(store, agent);
        async Task Add(Guid run, ScenarioRow scenario, int sample, string? error)
        {
            await store.AddSampleAsync(
                run,
                new NewSample(
                    scenario.Id,
                    scenario.Revision,
                    sample,
                    error is null ? SampleStatus.Done : SampleStatus.Failed,
                    error is null ? "[]" : null,
                    error,
                    null,
                    [],
                    []
                ),
                Cancellation
            );
        }
        await Add(first, refund, 1, "HTTP 500 first");
        await Add(first, refund, 2, null);
        await Add(first, chat, 1, "timeout");
        await Add(first, fine, 1, null);
        await Add(second, refund, 1, "HTTP 502 newest");
        await Add(second, refund, 2, "HTTP 503 newest too");
        await Add(second, chat, 1, null);
        await Add(second, fine, 1, null);
        await Add(outside, fine, 1, "not in the window");

        var errors = await new AgentErrorStore(database.Pool).ListAgentErrorsAsync([first, second], Cancellation);

        Assert.Equal(
            [("refund", 3, 4, second, "HTTP 502 newest"), ("chat", 1, 2, first, "timeout"), ("fine", 0, 2, null, null)],
            [.. errors.Select(row => (row.ScenarioKey, row.Errors, row.Samples, row.NewestErrorRunId, row.NewestError))]
        );
        Assert.All(errors, row => Assert.Equal(agent.VersionId, row.AgentVersionId));
        Assert.Empty(await new AgentErrorStore(database.Pool).ListAgentErrorsAsync([], Cancellation));
    }
}
