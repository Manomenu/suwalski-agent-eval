using Suwageval.AgentSdk.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Tests.Agents;

public sealed class AgentContractHashTests
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
        Assert.Equal(AgentContractHash.Of(Contract() with { Label = "v1" }), AgentContractHash.Of(Contract() with { Label = "v2" }));
        Assert.DoesNotContain("v1", AgentContractHash.Content(Contract() with { Label = "v1" }), StringComparison.Ordinal);
    }

    [Fact]
    public void Changed_content_is_another_hash() =>
        Assert.NotEqual(AgentContractHash.Of(Contract()), AgentContractHash.Of(Contract("Be brief.")));

    [Fact]
    public void Other_config_is_part_of_the_content() =>
        Assert.NotEqual(AgentContractHash.Of(Contract()), AgentContractHash.Of(Contract() with { OtherConfig = "retries: 2" }));

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
            AgentContractHash.Of(WithRetrieval("v1", "1832 articles")),
            AgentContractHash.Of(WithRetrieval("v1", "1900 articles"))
        );
        Assert.DoesNotContain("1832", AgentContractHash.Content(WithRetrieval("v1", "1832 articles")), StringComparison.Ordinal);
        Assert.NotEqual(AgentContractHash.Of(WithRetrieval("v1")), AgentContractHash.Of(WithRetrieval("v2")));
    }

    [Fact]
    public void The_hash_is_the_one_the_server_registers_the_content_under() =>
        Assert.Equal(ContentHash.Of(AgentContractHash.Content(Contract())), AgentContractHash.Of(Contract()));

    /// <summary>
    /// Agents with the SDK send this hash to name their version, so a change to it splits every version an agent already sent
    /// from the one the server registers: change the expected value only together with a migration of the stored versions.
    /// </summary>
    [Fact]
    public void The_hash_of_a_known_contract_does_not_change() =>
        Assert.Equal(
            "b2094ee4cb33332f6954d5d4c0a1116f89e9b166dd8e2a1f338fde914e7bab11",
            AgentContractHash.Of(WithRetrieval("v1", "1832 articles"))
        );
}
