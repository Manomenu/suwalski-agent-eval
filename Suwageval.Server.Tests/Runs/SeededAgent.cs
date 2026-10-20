using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Database;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Tests.Agents;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Runs;

/// <summary>An agent with a version, scenarios and checks in the test database, written the way the contracts say the slices store them.</summary>
internal sealed record SeededAgent(Guid Id, string Name, Guid VersionId, string EndpointUrl)
{
    /// <summary>The tools and event of the example agent.</summary>
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
                            Parameters = Json( /*lang=json,strict*/
                                """{"type":"object"}"""
                            ),
                        },
                        new ToolDefinition
                        {
                            Name = "refund_order",
                            Description = "Refund an order.",
                            Parameters = Json( /*lang=json,strict*/
                                """{"type":"object"}"""
                            ),
                        },
                    ],
                },
            ],
            Context = new ContextDefinition
            {
                Events =
                [
                    new EventDeclaration
                    {
                        Type = "state.changed",
                        DataSchema = Json( /*lang=json,strict*/
                            """{"type":"object"}"""
                        ),
                    },
                ],
            },
        };

    /// <summary>The example agent redeployed with another config: another version.</summary>
    public static AgentContract ExampleVersionConfigured(string config) =>
        ExampleVersion with
        {
            Agents = [ExampleVersion.Agents[0] with { Config = config }],
        };

    /// <summary>A contract as an agent serves it.</summary>
    public static string Serialized(AgentContract contract) => JsonSerializer.Serialize(contract, ContractJson.Options);

    /// <summary>The example agent, now declaring that it accepts fixtures.</summary>
    public static AgentContract ExampleVersionWithFixtures { get; } =
        ExampleVersion with
        {
            Context = ExampleVersion.Context with
            {
                FixturesSchema = Json( /*lang=json,strict*/
                    """{"type":"object"}"""
                ),
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
            Args = Json( /*lang=json,strict*/
                """{"order_id":"A-17"}"""
            ),
            Where = new CheckWhere { After = "get_order" },
            PassThreshold = passThreshold,
        };

    public static EventCheck RefundedEvent(string key, string scenarioKey) =>
        new()
        {
            Key = key,
            ScenarioKey = scenarioKey,
            Event = "state.changed",
            Data = Json( /*lang=json,strict*/
                """{"node":"refunded"}"""
            ),
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
            await agents.SetSettingsAsync(agent.Id, agentSamples, null, TestContext.Current.CancellationToken);
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
