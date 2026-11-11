using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Database;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Tests.Agents;
using static Suwageval.Server.Tests.Evaluation.Checks.Items;

namespace Suwageval.Server.Tests.Evaluation.Runs;

internal sealed record SeededAgent(Guid Id, string Name, Guid VersionId, string EndpointUrl)
{
    public static AgentContract ExampleVersion { get; } =
        new()
        {
            Agents =
            [
                new LlmAgentDefinition
                {
                    Name = "support-agent",
                    Model = new ModelDefinition { Name = "test-model" },
                    Config = "You are a support agent.",
                    Tools =
                    [
                        new ToolDefinition
                        {
                            Name = "get_order",
                            Description = "Look an order up.",
                            Parameters = Json("""{"type":"object"}"""),
                        },
                        new ToolDefinition
                        {
                            Name = "refund_order",
                            Description = "Refund an order.",
                            Parameters = Json("""{"type":"object"}"""),
                        },
                    ],
                },
            ],
            Context = new ContextDefinition
            {
                Events = [new EventDeclaration { Type = "state.changed", DataSchema = Json("""{"type":"object"}""") }],
            },
        };

    public static AgentContract ExampleVersionConfigured(string config) =>
        ExampleVersion with
        {
            Agents = [ExampleVersion.Agents[0] with { Config = config }],
        };

    public static string Serialized(AgentContract contract) => JsonSerializer.Serialize(contract, ContractJson.Options);

    public static AgentContract ExampleVersionWithFixtures { get; } =
        ExampleVersion with
        {
            Context = ExampleVersion.Context with { FixturesSchema = Json("""{"type":"object"}""") },
        };

    public static AgentContract ExampleVersionWithSystemTool { get; } =
        ExampleVersion with
        {
            Context = ExampleVersion.Context with
            {
                Tools =
                [
                    new ToolDefinition
                    {
                        Name = "book_visit",
                        Description = "Book the visit the procedure arranged.",
                        Parameters = Json("""{"type":"object"}"""),
                    },
                ],
            },
        };

    public static ScenarioInput Scenario(
        string key,
        string[]? tags = null,
        int? samples = 1,
        int turns = 1,
        JsonElement? fixtures = null,
        string? simulatedUser = null,
        IReadOnlyList<ConversationItem>? conversation = null
    ) =>
        new()
        {
            Key = key,
            Tags = tags ?? ["regression"],
            Conversation = conversation ?? [new UserMessage { Text = "Please refund order A-17." }],
            Continuation = new Continuation { Turns = turns, SimulatedUser = simulatedUser },
            Samples = samples,
            Fixtures = fixtures,
        };

    public static ToolCalledCheck RefundAfterLookup(string key, string scenarioKey, double passThreshold = 1.0) =>
        new()
        {
            Key = key,
            ScenarioKey = scenarioKey,
            Tool = "refund_order",
            Args = Json("""{"order_id":"A-17"}"""),
            Where = new CheckWhere { After = "get_order" },
            PassThreshold = passThreshold,
        };

    public static EventCheck RefundedEvent(string key, string scenarioKey) =>
        new()
        {
            Key = key,
            ScenarioKey = scenarioKey,
            Event = "state.changed",
            Data = Json("""{"node":"refunded"}"""),
        };

    public static async Task<SeededAgent> CreateAsync(
        Pool pool,
        string endpointUrl,
        IReadOnlyList<ScenarioInput> scenarios,
        IReadOnlyList<CheckInput> checks,
        AgentContract? version = null,
        int? agentSamples = null
    )
    {
        var agents = new AgentStore(pool);
        var agent = (await agents.CreateAsync(AgentStoreTests.UniqueName(), endpointUrl, TestContext.Current.CancellationToken)).Agent;
        var registered = await agents.RegisterVersionAsync(
            agent.Id,
            JsonSerializer.Serialize(version ?? ExampleVersion, ContractJson.Options),
            null,
            cancellationToken: TestContext.Current.CancellationToken
        );
        if (agentSamples is not null)
        {
            await agents.SetSettingsAsync(agent.Id, agentSamples, null, cancellationToken: TestContext.Current.CancellationToken);
        }
        await new ScenarioStore(pool).UpsertAsync(
            agent.Id,
            [
                .. scenarios.Select(scenario => new ScenarioItem(
                    scenario.Key,
                    scenario.Tags,
                    JsonSerializer.Serialize(scenario, ContractJson.Options)
                )),
            ],
            TestContext.Current.CancellationToken
        );
        await new CheckStore(pool).UpsertAsync(
            agent.Id,
            [.. checks.Select(check => new CheckItem(check.Key, check.ScenarioKey, JsonSerializer.Serialize(check, ContractJson.Options)))],
            TestContext.Current.CancellationToken
        );
        return new SeededAgent(agent.Id, agent.Name, registered.Version.Id, endpointUrl);
    }
}
