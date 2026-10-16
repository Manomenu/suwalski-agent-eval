using System.Text.Json;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Tests.Agents;

public sealed class ContractContentTests
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

    [Fact]
    public void The_label_is_no_part_of_the_content_so_renaming_alone_is_the_same_version()
    {
        Assert.Equal(ContractContent.Hash(Contract() with { Label = "v1" }), ContractContent.Hash(Contract() with { Label = "v2" }));
        Assert.DoesNotContain("v1", ContractContent.Json(Contract() with { Label = "v1" }), StringComparison.Ordinal);
    }

    [Fact]
    public void Changed_content_is_another_hash() =>
        Assert.NotEqual(ContractContent.Hash(Contract()), ContractContent.Hash(Contract("Be brief.")));

    [Fact]
    public void A_valid_contract_has_no_problems() => Assert.Empty(ContractContent.Problems(Contract()));

    [Fact]
    public void Other_config_is_part_of_the_content() =>
        Assert.NotEqual(ContractContent.Hash(Contract()), ContractContent.Hash(Contract() with { OtherConfig = "retries: 2" }));

    [Fact]
    public void The_model_names_are_those_of_all_agents_without_repeats_in_the_agents_order() =>
        Assert.Equal(
            ["large", "small"],
            ContractContent.ModelNames(
                new AgentContract { Agents = [Agent("a", model: "large"), Agent("b", model: "small"), Agent("c", model: "large")] }
            )
        );

    [Fact]
    public void An_empty_config_is_fine() => Assert.Empty(ContractContent.Problems(Contract("")));

    [Fact]
    public void Two_agents_may_share_a_tool_name() =>
        Assert.Empty(ContractContent.Problems(new AgentContract { Agents = [Agent("a", tools: Tool("t")), Agent("b", tools: Tool("t"))] }));

    [Fact]
    public void An_unnamed_or_repeated_agent_an_empty_model_and_repeated_tools_and_events_are_problems()
    {
        var @event = new EventDeclaration { Type = "e", DataSchema = Tool("t").Parameters };

        var problems = ContractContent.Problems(
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

    private static AgentContract WithRetrieval(string dataVersion, string? description = null) =>
        Contract() with
        {
            Context = new ContextDefinition
            {
                UsesRetrieval = true,
                Retrieval = new RetrievalDefinition { DataVersion = dataVersion, Description = description },
            },
        };

    [Fact]
    public void The_description_of_the_retrieval_data_is_no_part_of_the_content_but_its_version_is()
    {
        Assert.Equal(
            ContractContent.Hash(WithRetrieval("v1", "1832 articles")),
            ContractContent.Hash(WithRetrieval("v1", "1900 articles"))
        );
        Assert.DoesNotContain("1832", ContractContent.Json(WithRetrieval("v1", "1832 articles")), StringComparison.Ordinal);
        Assert.NotEqual(ContractContent.Hash(WithRetrieval("v1")), ContractContent.Hash(WithRetrieval("v2")));
        Assert.Equal("v2", ContractContent.DataVersion(WithRetrieval("v2")));
        Assert.Null(ContractContent.DataVersion(Contract()));
    }

    [Fact]
    public void Retrieval_with_a_data_version_is_fine() => Assert.Empty(ContractContent.Problems(WithRetrieval("v1")));

    [Fact]
    public void Using_retrieval_without_a_data_version_is_a_problem()
    {
        var missing = Contract() with { Context = new ContextDefinition { UsesRetrieval = true } };

        Assert.StartsWith("context.retrieval.dataVersion", Assert.Single(ContractContent.Problems(missing)), StringComparison.Ordinal);
        Assert.StartsWith(
            "context.retrieval.dataVersion",
            Assert.Single(ContractContent.Problems(WithRetrieval(" "))),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void A_data_version_without_using_retrieval_is_a_problem()
    {
        var contract = Contract() with { Context = new ContextDefinition { Retrieval = new RetrievalDefinition { DataVersion = "v1" } } };

        Assert.StartsWith("context.retrieval:", Assert.Single(ContractContent.Problems(contract)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_contract_with_no_agents_is_a_problem()
    {
        var problem = Assert.Single(ContractContent.Problems(new AgentContract { Agents = [] }));

        Assert.StartsWith("agents", problem, StringComparison.Ordinal);
    }
}
