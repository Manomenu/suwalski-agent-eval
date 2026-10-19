using System.Text.Json;
using Suwageval.Contracts.Agents;

namespace Suwageval.Server.Tests.Agents;

public sealed class AgentContractRulesTests
{
    private static AgentContract Contract(string config = "Be helpful.") => new() { Agents = [Agent("main", config)] };

    private static LlmAgentDefinition Agent(string name, string config = "c", string model = "some-model", params ToolDefinition[] tools) =>
        new()
        {
            Name = name,
            Model = new ModelDefinition { Name = model },
            Config = config,
            Tools = tools,
        };

    private static ToolDefinition Tool(string name) =>
        new()
        {
            Name = name,
            Description = "d",
            Parameters = JsonDocument.Parse("{}").RootElement,
        };

    private static AgentContract WithRetrieval(string dataVersion) =>
        Contract() with
        {
            Context = new ContextDefinition
            {
                UsesRetrieval = true,
                Retrieval = new RetrievalDefinition { DataVersion = dataVersion },
            },
        };

    [Fact]
    public void A_valid_contract_has_no_problems() => Assert.Empty(AgentContractRules.Problems(Contract()));

    [Fact]
    public void An_empty_config_is_fine() => Assert.Empty(AgentContractRules.Problems(Contract("")));

    [Fact]
    public void Two_agents_may_share_a_tool_name() =>
        Assert.Empty(
            AgentContractRules.Problems(new AgentContract { Agents = [Agent("a", tools: Tool("t")), Agent("b", tools: Tool("t"))] })
        );

    [Fact]
    public void An_unnamed_or_repeated_agent_an_empty_model_and_repeated_tools_and_events_are_problems()
    {
        var @event = new EventDeclaration { Type = "e", DataSchema = Tool("t").Parameters };

        var problems = AgentContractRules.Problems(
            new AgentContract
            {
                Agents = [Agent("a", model: " ", tools: [Tool("t"), Tool("t")]), Agent("a"), Agent(" ")],
                Context = new ContextDefinition { Events = [@event, @event] },
            }
        );

        Assert.Equal(5, problems.Count);
        Assert.StartsWith("agents[0].model.name", problems[0], StringComparison.Ordinal);
        Assert.StartsWith("agents[0].tools[1].name", problems[1], StringComparison.Ordinal);
        Assert.StartsWith("agents[1].name", problems[2], StringComparison.Ordinal);
        Assert.StartsWith("agents[2].name", problems[3], StringComparison.Ordinal);
        Assert.StartsWith("context.events[1].type", problems[4], StringComparison.Ordinal);
    }

    [Fact]
    public void Retrieval_with_a_data_version_is_fine() => Assert.Empty(AgentContractRules.Problems(WithRetrieval("v1")));

    [Fact]
    public void Using_retrieval_without_a_data_version_is_a_problem()
    {
        var missing = Contract() with { Context = new ContextDefinition { UsesRetrieval = true } };

        Assert.StartsWith("context.retrieval.dataVersion", Assert.Single(AgentContractRules.Problems(missing)), StringComparison.Ordinal);
        Assert.StartsWith(
            "context.retrieval.dataVersion",
            Assert.Single(AgentContractRules.Problems(WithRetrieval(" "))),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void A_data_version_without_using_retrieval_is_a_problem()
    {
        var contract = Contract() with { Context = new ContextDefinition { Retrieval = new RetrievalDefinition { DataVersion = "v1" } } };

        Assert.StartsWith("context.retrieval:", Assert.Single(AgentContractRules.Problems(contract)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_contract_with_no_agents_is_a_problem()
    {
        var problem = Assert.Single(AgentContractRules.Problems(new AgentContract { Agents = [] }));

        Assert.StartsWith("agents", problem, StringComparison.Ordinal);
    }
}
