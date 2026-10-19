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
        public Task<TurnResponse> HandleAsync(TurnRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new TurnResponse { Items = [new AssistantMessage { Text = ((UserMessage)request.Conversation[^1]).Text }] });
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
        using var response = await PostTurn("""{"conversation":[{"text":"hi","kind":"UserMessage"}]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var turn = await response.Content.ReadFromJsonAsync<TurnResponse>(ContractJson.Options, Cancellation);
        Assert.Equal("hi", Assert.IsType<AssistantMessage>(Assert.Single(turn!.Items)).Text);
    }

    [Theory]
    [InlineData("""{"conversation":[]}""")]
    [InlineData("""{"conversation":[{"kind":"AssistantMessage","text":"hello"}]}""")]
    [InlineData("""{"conversation":[{"kind":"UserMessage","text":"hi"}],"typo":1}""")]
    public async Task A_request_that_is_not_a_turn_to_answer_is_a_400(string json)
    {
        using var response = await PostTurn(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
