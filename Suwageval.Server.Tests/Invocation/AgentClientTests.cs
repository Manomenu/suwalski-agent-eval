using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;
using Suwageval.Server.Invocation;

namespace Suwageval.Server.Tests.Invocation;

public sealed class AgentClientTests
{
    private static readonly Uri Agent = new("http://agent.test/base/");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly TurnContext Context = new(
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        "refund-flow",
        3,
        ScenarioType.Simulation,
        ["refunds", "tier-1"],
        ["smoke"],
        "sweep 1, a=b",
        "conv-1"
    );

    private sealed class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        public Dictionary<string, string> RequestHeaders { get; private set; } = [];

        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestHeaders = request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value));
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await answer(request);
        }
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static async Task<(TurnResult Result, Stub Stub)> CallAsync(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer)
    {
        using var stub = new Stub(answer);
        using var http = new HttpClient(stub, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
        var result = await new AgentClient(http).TurnAsync(Agent, [new UserMessage { Text = "hi" }], Context, Cancellation);
        return (result, stub);
    }

    [Fact]
    public async Task Posts_the_conversation_to_the_turn_route_and_reads_the_items()
    {
        var (result, stub) = await CallAsync(_ =>
            Task.FromResult(
                Ok(
                    /*lang=json,strict*/
                    """{"items":[{"kind":"ToolCall","id":"1","name":"get_order","arguments":{"order_id":"A-17"}},{"kind":"AssistantMessage","text":"ok"}]}"""
                )
            )
        );

        Assert.Equal(new Uri("http://agent.test/base/suwageval/turn"), stub.RequestUri);
        Assert.Equal( /*lang=json,strict*/
            """{"conversation":[{"kind":"UserMessage","text":"hi"}],"conversationId":"conv-1"}""",
            stub.RequestBody
        );
        Assert.Null(result.Error);
        Assert.Equal(2, result.Items?.Count);
        Assert.Equal("get_order", Assert.IsType<ToolCall>(result.Items?[0]).Name);
        Assert.Equal("ok", Assert.IsType<AssistantMessage>(result.Items?[1]).Text);
    }

    [Fact]
    public async Task Does_not_send_null_fields_the_agent_may_not_know()
    {
        using var stub = new Stub(_ =>
            Task.FromResult(
                Ok( /*lang=json,strict*/
                    """{"items":[]}"""
                )
            )
        );
        using var http = new HttpClient(stub, disposeHandler: false);

        await new AgentClient(http).TurnAsync(Agent, [new AssistantMessage { Text = "a" }], Context, Cancellation);

        Assert.DoesNotContain("interrupted", stub.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_the_fixtures_verbatim_and_the_correlation_headers_on_every_call()
    {
        using var stub = new Stub(_ =>
            Task.FromResult(
                Ok( /*lang=json,strict*/
                    """{"items":[]}"""
                )
            )
        );
        using var http = new HttpClient(stub, disposeHandler: false);
        var fixtures = JsonDocument
            .Parse( /*lang=json,strict*/
                """{"pinnedResources":["plans"],"n":1.50}"""
            )
            .RootElement.Clone();

        await new AgentClient(http).TurnAsync(Agent, [new UserMessage { Text = "hi" }], Context with { Fixtures = fixtures }, Cancellation);

        using var body = JsonDocument.Parse(stub.RequestBody ?? "");
        Assert.Equal(fixtures.GetRawText(), body.RootElement.GetProperty("fixtures").GetRawText());
        Assert.Equal("conv-1", body.RootElement.GetProperty("conversationId").GetString());
        Assert.DoesNotContain(stub.RequestHeaders.Keys, name => name.StartsWith("Suwageval-", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            "suwageval.run.id=11111111-2222-3333-4444-555555555555,suwageval.scenario.key=refund-flow,suwageval.scenario.type=Simulation,"
                + "suwageval.sample=3,suwageval.scenario.tags=refunds%2Ctier-1,suwageval.run.tags=smoke,suwageval.run.group=sweep%201%2C%20a=b",
            stub.RequestHeaders["baggage"]
        );
    }

    [Fact]
    public void Leaves_out_the_baggage_of_what_the_run_does_not_have()
    {
        var bare = Context with { ScenarioTags = [], RunTags = [], RunGroup = null, ScenarioType = ScenarioType.Turn };

        Assert.Equal(
            "suwageval.run.id=11111111-2222-3333-4444-555555555555,suwageval.scenario.key=refund-flow,suwageval.scenario.type=Turn,suwageval.sample=3",
            AgentClient.Baggage(bare)
        );
    }

    [Fact]
    public async Task Leaves_the_fixtures_out_when_the_scenario_has_none()
    {
        var (_, stub) = await CallAsync(_ =>
            Task.FromResult(
                Ok( /*lang=json,strict*/
                    """{"items":[]}"""
                )
            )
        );

        Assert.DoesNotContain("fixtures", stub.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Measures_how_long_the_call_took_whether_it_succeeded_or_not()
    {
        var (answered, _) = await CallAsync(async _ =>
        {
            await Task.Delay(60, Cancellation);
            return Ok( /*lang=json,strict*/
                """{"items":[]}"""
            );
        });
        var (failed, _) = await CallAsync(async _ =>
        {
            await Task.Delay(60, Cancellation);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        Assert.True(answered.DurationMs >= 50, $"{answered.DurationMs}");
        Assert.Null(failed.Items);
        Assert.True(failed.DurationMs >= 50, $"{failed.DurationMs}");
    }

    [Fact]
    public async Task An_error_status_is_a_failed_result_with_the_status_and_the_body()
    {
        var (result, _) = await CallAsync(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) { Content = new StringContent("bad conversation") })
        );

        Assert.Null(result.Items);
        Assert.Contains("422", result.Error, StringComparison.Ordinal);
        Assert.Contains("bad conversation", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData( /*lang=json,strict*/
        """{"items":[{"kind":"Nope"}]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"items":"x"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"items":[],"extra":1}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"items":[{"kind":"AssistantMessage"}]}"""
    )]
    [InlineData("null")]
    public async Task An_unreadable_answer_is_a_failed_result(string body)
    {
        var (result, _) = await CallAsync(_ => Task.FromResult(Ok(body)));

        Assert.Null(result.Items);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task An_answer_that_is_not_json_is_a_failed_result()
    {
        var (result, _) = await CallAsync(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>", Encoding.UTF8, "text/html") }
            )
        );

        Assert.Null(result.Items);
        Assert.Contains("could not be read", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_agent_that_cannot_be_reached_is_a_failed_result()
    {
        var (result, _) = await CallAsync(_ => throw new HttpRequestException("connection refused"));

        Assert.Null(result.Items);
        Assert.Contains("could not be reached", result.Error, StringComparison.Ordinal);
        Assert.Contains("connection refused", result.Error, StringComparison.Ordinal);
    }

    private sealed class Hanging : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task A_timeout_is_a_failed_result()
    {
        using var hanging = new Hanging();
        using var http = new HttpClient(hanging, disposeHandler: false) { Timeout = TimeSpan.FromMilliseconds(200) };

        var result = await new AgentClient(http).TurnAsync(Agent, [new UserMessage { Text = "hi" }], Context, Cancellation);

        Assert.Null(result.Items);
        Assert.Contains("did not answer within", result.Error, StringComparison.Ordinal);
    }

    private const string ContractBody = /*lang=json,strict*/
        """
        {"label":"abc1234","agents":[{"name":"main","model":{"name":"m","parameters":{"temperature":0}},"config":"Be helpful.",
         "tools":[{"name":"get_order","description":"d","parameters":{"type":"object"}}]}],"otherConfig":"retries: 2",
         "context":{"usesRetrieval":false,"events":[{"type":"state.changed","dataSchema":{"type":"object"}}]}}
        """;

    private static async Task<(ContractResult Result, Stub Stub)> ContractAsync(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer)
    {
        using var stub = new Stub(answer);
        using var http = new HttpClient(stub, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
        return (await new AgentClient(http).GetContractAsync(Agent, Cancellation), stub);
    }

    [Fact]
    public async Task Gets_the_contract_from_the_contract_route_and_reads_it()
    {
        var (result, stub) = await ContractAsync(_ => Task.FromResult(Ok(ContractBody)));

        Assert.Equal(new Uri("http://agent.test/base/suwageval/contract"), stub.RequestUri);
        Assert.Null(result.Error);
        var contract = result.Contract ?? throw new InvalidOperationException("no contract");
        var agent = Assert.Single(contract.Agents);
        Assert.Equal(("abc1234", "Be helpful.", "m"), (contract.Label, agent.Config, agent.Model.Name));
        Assert.Equal(("main", "retries: 2"), (agent.Name, contract.OtherConfig));
        Assert.Equal("get_order", Assert.Single(agent.Tools).Name);
        Assert.Equal("state.changed", Assert.Single(contract.Context.Events).Type);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData( /*lang=json,strict*/
        """{"otherConfig":"p"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"agents":[],"colour":"red"}"""
    )]
    public async Task An_unreadable_contract_is_a_failed_result_naming_the_url(string body)
    {
        var (result, _) = await ContractAsync(_ => Task.FromResult(Ok(body)));

        Assert.Null(result.Contract);
        Assert.Contains("http://agent.test/base/suwageval/contract", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_contract_route_answering_an_error_or_down_is_a_failed_result()
    {
        var (notFound, _) = await ContractAsync(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var (down, _) = await ContractAsync(_ => throw new HttpRequestException("connection refused"));

        Assert.Contains("404", notFound.Error, StringComparison.Ordinal);
        Assert.Contains("connection refused", down.Error, StringComparison.Ordinal);
        Assert.Contains("http://agent.test/base/suwageval/contract", down.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_contract_that_takes_too_long_is_a_failed_result()
    {
        using var hanging = new Hanging();
        using var http = new HttpClient(hanging, disposeHandler: false) { Timeout = TimeSpan.FromMilliseconds(200) };

        var result = await new AgentClient(http).GetContractAsync(Agent, Cancellation);

        Assert.Null(result.Contract);
        Assert.Contains("no answer within", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_call_stays_cancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        using var stub = new Stub(_ =>
            Task.FromResult(
                Ok( /*lang=json,strict*/
                    """{"items":[]}"""
                )
            )
        );
        using var http = new HttpClient(stub, disposeHandler: false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AgentClient(http).TurnAsync(Agent, [new UserMessage { Text = "hi" }], Context, cancelled.Token)
        );
    }
}
