using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;

namespace Suwageval.Server.Tests.Invocation;

public sealed class SuwagevalAgentEndpointsTests : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private sealed class FixedContract : IAgentContractSource
    {
        public Task<AgentContract> GetContractAsync(CancellationToken cancellationToken) =>
            Task.FromResult(
                new AgentContract
                {
                    Agents =
                    [
                        new LlmAgentDefinition
                        {
                            Name = "main",
                            Model = new ModelDefinition { Name = "m" },
                            Config = "",
                        },
                    ],
                }
            );
    }

    private sealed class EchoHandler : ITurnHandler
    {
        private static string Who(TurnCorrelation correlation) =>
            $"{correlation.RunId}/{correlation.Scenario}/{correlation.ScenarioType}/{correlation.Sample}/{string.Join('+', correlation.ScenarioTags)}/{string.Join('+', correlation.RunTags)}/{correlation.RunGroup}/{correlation.AgentVersionId}";

        public Task<TurnResponse> HandleAsync(TurnRequest request, CancellationToken cancellationToken)
        {
            var text = ((UserMessage)request.Conversation[^1]).Text;
            return text == "reject"
                ? throw new TurnRejectedException("Rejected by the handler.")
                : Task.FromResult(
                    new TurnResponse { Items = [new AssistantMessage { Text = text == "who" ? Who(request.Correlation) : text }] }
                );
        }
    }

    private WebApplication? app;
    private HttpClient? client;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IAgentContractSource>(new FixedContract());
        builder.Services.AddSingleton<ITurnHandler>(new EchoHandler());
        app = builder.Build();
        app.MapSuwagevalAgent();
        await app.StartAsync(Cancellation);
        client = app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (app is not null)
        {
            await app.DisposeAsync();
        }
    }

    private async Task<HttpResponseMessage> PostTurn(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await client!.PostAsync(new Uri("/suwageval/turn", UriKind.Relative), content, Cancellation);
    }

    [Fact]
    public async Task The_contract_is_served_and_a_turn_is_answered_in_the_contract_json()
    {
        var contract = await client!.GetFromJsonAsync<AgentContract>(
            new Uri("/suwageval/contract", UriKind.Relative),
            ContractJson.Options,
            Cancellation
        );
        Assert.Equal("main", Assert.Single(contract!.Agents).Name);

        // The discriminator comes last here: it must still be read.
        using var response = await PostTurn( /*lang=json,strict*/
            """{"conversation":[{"text":"hi","kind":"UserMessage"}]}"""
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var turn = await response.Content.ReadFromJsonAsync<TurnResponse>(ContractJson.Options, Cancellation);
        Assert.Equal("hi", Assert.IsType<AssistantMessage>(Assert.Single(turn!.Items)).Text);
    }

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"conversation":[]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"conversation":[{"kind":"AssistantMessage","text":"hello"}]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"conversation":[{"kind":"UserMessage","text":"hi"}],"typo":1}"""
    )]
    public async Task A_request_that_is_not_a_turn_to_answer_is_a_400(string json)
    {
        using var response = await PostTurn(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_turn_the_handler_rejects_is_a_400_with_its_reason()
    {
        using var response = await PostTurn( /*lang=json,strict*/
            """{"conversation":[{"kind":"UserMessage","text":"reject"}]}"""
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Rejected by the handler.", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_baggage_reaches_the_handler_and_absent_ones_are_null()
    {
        using var content = new StringContent(
            /*lang=json,strict*/
            """{"conversation":[{"kind":"UserMessage","text":"who"}]}""",
            System.Text.Encoding.UTF8,
            "application/json"
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/suwageval/turn", UriKind.Relative)) { Content = content };
        request.Headers.Add(
            SuwagevalBaggage.HeaderName,
            "suwageval.run.id=run-1,suwageval.agent.version.id=v-9,suwageval.scenario.sample=2,suwageval.scenario.type=Simulation,suwageval.scenario.tags=a%2Cb,other=x"
        );

        using var response = await client!.SendAsync(request, Cancellation);

        var turn = await response.Content.ReadFromJsonAsync<TurnResponse>(ContractJson.Options, Cancellation);
        Assert.Equal("run-1//Simulation/2/a+b///v-9", Assert.IsType<AssistantMessage>(Assert.Single(turn!.Items)).Text);
    }
}
