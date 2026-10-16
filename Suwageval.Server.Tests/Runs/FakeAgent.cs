using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Suwageval.Server.Tests.Runs;

/// <summary>
/// An agent under test that answers from a script, on a free local port: a real HTTP server, so the whole path of a call
/// (client, timeout, JSON) runs, but no model is ever involved.
/// </summary>
internal sealed class FakeAgent : IAsyncDisposable
{
    /// <summary>A turn: a refund after a lookup, with the event and the final message.</summary>
    public const string RefundTurn = /*lang=json,strict*/
        """
        {"items":[
          {"kind":"ToolCall","id":"c1","name":"get_order","arguments":{"order_id":"A-17"}},
          {"kind":"ToolResult","callId":"c1","content":"{\"status\":\"delivered\"}","isError":false},
          {"kind":"ToolCall","id":"c2","name":"refund_order","arguments":{"order_id":"A-17","reason":"broken"}},
          {"kind":"ToolResult","callId":"c2","content":"{\"orderId\":\"A-17\"}","isError":false},
          {"kind":"Event","type":"state.changed","data":{"node":"refunded","orderId":"A-17"}},
          {"kind":"AssistantMessage","text":"Your refund for A-17 is on its way."}
        ]}
        """;

    /// <summary>A turn that only answers: no tool, no event.</summary>
    public const string PlainTurn = /*lang=json,strict*/
        """{"items":[{"kind":"AssistantMessage","text":"Hello, how can I help?"}]}""";

    private WebApplication? app;
    private readonly ConcurrentQueue<string> requests = new();
    private int inFlight;
    private int maxInFlight;

    private FakeAgent() { }

    public Uri Url { get; private set; } = new("http://invalid.test");

    /// <summary>The bodies of the requests received so far.</summary>
    public IReadOnlyCollection<string> Requests => requests;

    /// <summary>The most calls that were being answered at the same moment.</summary>
    public int MaxConcurrency => Volatile.Read(ref maxInFlight);

    /// <param name="answer">Gets the request body and the number of the call (from 1); returns the response.</param>
    /// <param name="delay">How long each call takes.</param>
    public static async Task<FakeAgent> StartAsync(Func<string, int, IResult> answer, TimeSpan? delay = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var agent = new FakeAgent();
        app.MapPost(
            "/suwageval/turn",
            async (HttpRequest request) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                agent.requests.Enqueue(body);
                var now = Interlocked.Increment(ref agent.inFlight);
                int seen;
                do
                {
                    seen = Volatile.Read(ref agent.maxInFlight);
                } while (now > seen && Interlocked.CompareExchange(ref agent.maxInFlight, now, seen) != seen);
                try
                {
                    if (delay is { } wait)
                    {
                        await Task.Delay(wait);
                    }
                    return answer(body, agent.requests.Count);
                }
                finally
                {
                    Interlocked.Decrement(ref agent.inFlight);
                }
            }
        );
        await app.StartAsync();
        agent.app = app;
        agent.Url = new Uri(app.Urls.First());
        return agent;
    }

    /// <summary>An agent that always answers with the same JSON.</summary>
    public static Task<FakeAgent> AlwaysAsync(string json, TimeSpan? delay = null) =>
        StartAsync((_, _) => Results.Text(json, "application/json"), delay);

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
