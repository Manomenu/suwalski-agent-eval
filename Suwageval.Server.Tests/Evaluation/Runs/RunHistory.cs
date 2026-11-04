using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.Tests.Evaluation.Runs;

internal static class RunHistory
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static async Task<Guid> NewerVersionAsync(IServiceProvider services, SeededAgent agent, string prompt)
    {
        var registration = await services
            .GetRequiredService<AgentStore>()
            .RegisterVersionAsync(agent.Id, AgentContractHash.Content(SeededAgent.ExampleVersionConfigured(prompt)), null, Cancellation);
        return registration.Version.Id;
    }

    public static async Task AddRunAsync(IServiceProvider services, SeededAgent agent, Guid versionId, bool? passed)
    {
        var store = services.GetRequiredService<RunStore>();
        var scenario = (await services.GetRequiredService<ScenarioStore>().GetAsync(agent.Id, "refund", Cancellation))!;
        var check = (await services.GetRequiredService<CheckStore>().ListAsync(agent.Id, cancellationToken: Cancellation)).First(check =>
            check.Key == "refund-called"
        );
        var id = await store.EnqueueAsync(agent.Id, versionId, [], agent.EndpointUrl, cancellationToken: Cancellation);
        await store.AddSampleAsync(
            id,
            passed is null
                ? new NewSample(scenario.Id, scenario.Revision, 1, SampleStatus.Failed, null, "HTTP 500", null, [], [])
                : new NewSample(
                    scenario.Id,
                    scenario.Revision,
                    1,
                    SampleStatus.Done,
                    "[]",
                    null,
                    1,
                    [],
                    [new NewCheckResult(check.Id, check.Revision, passed.Value, "[]", "m")]
                ),
            Cancellation
        );
        await store.FinishAsync(id, RunStatus.Done, cancellationToken: Cancellation);
    }
}
