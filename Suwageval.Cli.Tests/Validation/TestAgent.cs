using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Tests.Validation;

internal static class TestAgent
{
    public const string Yaml = """
        name: shop-agent
        endpointUrl: http://localhost:7010
        """;

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

    public static string[] Lines(IEnumerable<Problem> problems) => [.. problems.Select(p => $"{p.Path}: {p.Message}")];

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
