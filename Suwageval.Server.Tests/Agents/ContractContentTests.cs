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
    }
}
