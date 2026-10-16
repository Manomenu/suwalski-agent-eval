using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Cli.Files;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Tests.Validation;

/// <summary>A small agent the validation tests check scenarios against, and a way to read what they report.</summary>
internal static class TestAgent
{
    public const string Yaml = """
        name: shop-agent
        endpointUrl: http://localhost:7010
        """;

    /// <summary>What the running agent serves for <see cref="Yaml"/>: two agents with three tools, and one event.</summary>
    public static AgentContract Contract { get; } =
        new()
        {
            Agents =
            [
                new LlmAgentDefinition
                {
                    Name = "support-agent",
                    Model = new ModelDefinition { Name = "some-model" },
                    Config = "Be helpful.",
                    Tools =
                    [
                        new ToolDefinition
                        {
                            Name = "get_order",
                            Description = "Look an order up.",
                            Parameters = Schema,
                        },
                        new ToolDefinition
                        {
                            Name = "refund_order",
                            Description = "Refund an order.",
                            Parameters = Schema,
                        },
                    ],
                },
                new LlmAgentDefinition
                {
                    Name = "billing-agent",
                    Model = new ModelDefinition { Name = "some-model" },
                    Config = "Handle payments.",
                    Tools =
                    [
                        new ToolDefinition
                        {
                            Name = "charge_card",
                            Description = "Charge a card.",
                            Parameters = Schema,
                        },
                    ],
                },
            ],
            Context = new ContextDefinition { Events = [new EventDeclaration { Type = "state.changed", DataSchema = Schema }] },
        };

    /// <summary>The same agent, now declaring that it accepts fixtures.</summary>
    public static AgentContract WithFixtures { get; } = Contract with { Context = Contract.Context with { FixturesSchema = Schema } };

    public static AgentContract WithRetrieval { get; } =
        new()
        {
            Agents =
            [
                new LlmAgentDefinition
                {
                    Name = "support-agent",
                    Model = new ModelDefinition { Name = "some-model" },
                    Config = "Be helpful.",
                },
            ],
            Context = new ContextDefinition
            {
                UsesRetrieval = true,
                Retrieval = new RetrievalDefinition { DataVersion = "test-data" },
            },
        };

    public const string Scenario = """
        key: refund-happy-path
        tags: [refunds]
        conversation:
          - user: "Refund A-17, please."
        checks:
          - key: refund-called
            type: tool-called
            tool: refund_order
        """;

    private static JsonElement Schema => JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone();

    /// <summary>Each problem as <c>path: message</c>, in the order found.</summary>
    public static string[] Lines(IEnumerable<Problem> problems) => [.. problems.Select(p => $"{p.Path}: {p.Message}")];

    /// <summary>A stand-in for the running agent: answers <c>GET …/suwageval/contract</c> with the contract, or as the handler says.</summary>
    public static HttpClient AgentServing(AgentContract contract, List<Uri>? requested = null) =>
        Agent(request =>
        {
            requested?.Add(request.RequestUri!);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(contract, ContractJson.Options), Encoding.UTF8, "application/json"),
            };
        });

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The client owns its handler and disposes it."
    )]
    public static HttpClient Agent(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new Handler(answer), disposeHandler: true) { Timeout = TimeSpan.FromSeconds(5) };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
