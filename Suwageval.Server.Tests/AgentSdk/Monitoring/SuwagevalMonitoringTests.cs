using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;
using Suwageval.AgentSdk.Monitoring;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Monitoring;
using Suwageval.Server.Tests.Agents;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests.AgentSdk.Monitoring;

/// <summary>The SDK's monitoring as an agent uses it: turns recorded in the agent reach the server in process, in the background.</summary>
public sealed class SuwagevalMonitoringTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => app.DisposeAsync();

    /// <summary>What the edge (nginx, the vite proxy) does in front of the server: the API's <c>/api</c> prefix comes off.</summary>
    private sealed class EdgeHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            request.RequestUri = new UriBuilder(uri) { Path = uri.AbsolutePath["/api".Length..] }.Uri;
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class FixedContract(AgentContract contract) : IAgentContractSource
    {
        public Task<AgentContract> GetContractAsync(CancellationToken cancellationToken) => Task.FromResult(contract);
    }

    /// <summary>The services of an agent with monitoring set up, sending to the server in process.</summary>
    private ServiceProvider Agent(string name)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Suwageval:Url"] = "http://suwageval.test",
                    ["Suwageval:Agent"] = name,
                    ["Suwageval:Environment"] = "production",
                }
            )
            .Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IAgentContractSource>(new FixedContract(AgentEndpointsTests.Version()));
        services.AddSuwagevalMonitoring(configuration);
        services
            .AddHttpClient(SuwagevalMonitoring.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new EdgeHandler { InnerHandler = app.Server.CreateHandler() });
        return services.BuildServiceProvider();
    }

    private async Task<string> MonitoredAgent()
    {
        _ = database.Pool;
        var name = AgentStoreTests.UniqueName();
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/agents", UriKind.Relative),
            new AgentDefinition
            {
                Name = name,
                EndpointUrl = "http://a.test",
                Features = new AgentFeatures { Monitoring = new MonitoringSettings() },
            },
            ContractJson.Options,
            Cancellation
        );
        response.EnsureSuccessStatusCode();
        return name;
    }

    private async Task<ConversationList> WaitForConversationsAsync(string name, int count)
    {
        using var client = app.CreateClient();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var list = await client.GetFromJsonAsync<ConversationList>(
                new Uri($"/monitoring/agents/{name}/conversations", UriKind.Relative),
                ContractJson.Options,
                Cancellation
            );
            if (list!.Conversations.Count >= count || DateTime.UtcNow > deadline)
            {
                return list;
            }
            await Task.Delay(200, Cancellation);
        }
    }

    [Fact]
    public async Task Recorded_turns_reach_the_server_with_the_agents_version_and_a_turn_of_an_evaluation_run_does_not()
    {
        var name = await MonitoredAgent();
        await using var agent = Agent(name);
        var hosted = agent.GetServices<IHostedService>().ToList();
        foreach (var service in hosted)
        {
            await service.StartAsync(Cancellation);
        }
        var recorder = agent.GetRequiredService<IConversationRecorder>();

        using (var evaluation = new Activity("turn"))
        {
            evaluation.SetBaggage(SuwagevalBaggage.RunId, Guid.NewGuid().ToString()).Start();
            recorder.Record("from-a-run", [new UserMessage { Text = "A scenario" }, new AssistantMessage { Text = "Tested" }]);
        }
        recorder.Record("real", [new UserMessage { Text = "Where is A-17?" }, new AssistantMessage { Text = "On its way." }], "index-7");
        var list = await WaitForConversationsAsync(name, 1);
        foreach (var service in hosted)
        {
            await service.StopAsync(Cancellation);
        }

        var conversation = Assert.Single(list.Conversations);
        Assert.Equal(("real", "production", "index-7"), (conversation.ConversationId, conversation.Environment, conversation.DataVersion));
        using var client = app.CreateClient();
        var versions = await client.GetFromJsonAsync<AgentVersion[]>(
            new Uri($"/agents/{name}/versions", UriKind.Relative),
            ContractJson.Options,
            Cancellation
        );
        Assert.Equal(AgentContractHash.Of(AgentEndpointsTests.Version()), Assert.Single(versions!).ContentHash);
    }

    [Fact]
    public void Without_a_url_the_recorder_records_nothing_and_with_one_the_agent_and_environment_are_required()
    {
        var empty = new ConfigurationBuilder().Build();
        using var off = new ServiceCollection().AddSuwagevalMonitoring(empty).BuildServiceProvider();
        off.GetRequiredService<IConversationRecorder>().Record("c", [new UserMessage { Text = "Hi" }]);
        Assert.Empty(off.GetServices<IHostedService>());

        var incomplete = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Suwageval:Url"] = "http://suwageval.test" })
            .Build();
        using var broken = new ServiceCollection().AddLogging().AddSuwagevalMonitoring(incomplete).BuildServiceProvider();
        var error = Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            broken.GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitoringOptions>>().Value
        );
        Assert.Contains("Suwageval:Agent", error.Message, StringComparison.Ordinal);
    }
}
