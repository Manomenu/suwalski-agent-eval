using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Suwageval.AgentSdk.Agents;

namespace Suwageval.Server.Tests.Evaluation.Runs;

internal sealed class FakeAgent : IAsyncDisposable
{
    public const string RefundTurn = """
        {"items":[
          {"kind":"ToolCall","id":"c1","name":"get_order","arguments":{"order_id":"A-17"}},
          {"kind":"ToolResult","callId":"c1","content":"{\"status\":\"delivered\"}","isError":false},
          {"kind":"ToolCall","id":"c2","name":"refund_order","arguments":{"order_id":"A-17","reason":"broken"}},
          {"kind":"ToolResult","callId":"c2","content":"{\"orderId\":\"A-17\"}","isError":false},
          {"kind":"Event","type":"state.changed","data":{"node":"refunded","orderId":"A-17"}},
          {"kind":"AssistantMessage","text":"Your refund for A-17 is on its way."}
        ]}
        """;

    public const string PlainTurn = """{"items":[{"kind":"AssistantMessage","text":"Hello, how can I help?"}]}""";

    private WebApplication? app;
    private readonly ConcurrentQueue<string> requests = new();
    private readonly ConcurrentQueue<IReadOnlyDictionary<string, string>> headers = new();
    private int inFlight;
    private int maxInFlight;
    private int contractRequests;
    private Func<int, IResult> contract = _ => Results.Text(SeededAgent.Serialized(SeededAgent.ExampleVersion), "application/json");

    private FakeAgent() { }

    public Uri Url { get; private set; } = new("http://invalid.test");

    public IReadOnlyCollection<string> Requests => requests;

    public IReadOnlyCollection<IReadOnlyDictionary<string, string>> RequestHeaders => headers;

    public int ContractRequests => Volatile.Read(ref contractRequests);

    public void ServeContract(AgentContract served) => ServeContract(_ => Results.Text(SeededAgent.Serialized(served), "application/json"));

    public void ServeContract(Func<int, IResult> answer) => Volatile.Write(ref contract, answer);

    public int MaxConcurrency => Volatile.Read(ref maxInFlight);

    public static Task<FakeAgent> StartAsync(Func<string, int, IResult> answer, TimeSpan? delay = null) =>
        StartAsync((body, call) => Task.FromResult(answer(body, call)), delay);

    public static async Task<FakeAgent> StartAsync(Func<string, int, Task<IResult>> answer, TimeSpan? delay = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var agent = new FakeAgent();
        app.MapGet("/suwageval/contract", () => Volatile.Read(ref agent.contract)(Interlocked.Increment(ref agent.contractRequests)));
        app.MapPost(
            "/suwageval/turn",
            async (HttpRequest request) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                agent.headers.Enqueue(
                    request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase)
                );
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
                    return await answer(body, agent.requests.Count);
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
