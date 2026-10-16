using System.Net;
using System.Text;
using Suwageval.Contracts.Conversations;
using Suwageval.Server.Invocation;

namespace Suwageval.Server.Tests.Invocation;

public sealed class AgentClientTests
{
    private static readonly Uri Agent = new("http://agent.test/base/");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private sealed class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
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
        var result = await new AgentClient(http).TurnAsync(Agent, [new UserMessage { Text = "hi" }], Cancellation);
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
            """{"conversation":[{"kind":"UserMessage","text":"hi"}]}""",
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

        await new AgentClient(http).TurnAsync(Agent, [new AssistantMessage { Text = "a" }], Cancellation);

        Assert.DoesNotContain("interrupted", stub.RequestBody, StringComparison.Ordinal);
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

        var result = await new AgentClient(http).TurnAsync(Agent, [new UserMessage { Text = "hi" }], Cancellation);

        Assert.Null(result.Items);
        Assert.Contains("did not answer within", result.Error, StringComparison.Ordinal);
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
            new AgentClient(http).TurnAsync(Agent, [new UserMessage { Text = "hi" }], cancelled.Token)
        );
    }
}
