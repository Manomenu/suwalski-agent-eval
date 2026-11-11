using System.Text.Json;
using Suwageval.AgentSdk;
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
    public void The_retrieval_data_is_no_part_of_the_content_so_new_data_is_the_same_version()
    {
        Assert.Equal(
            AgentContractHash.Of(WithRetrieval("v1", "1832 articles")),
            AgentContractHash.Of(WithRetrieval("v1", "1900 articles"))
        );
        Assert.DoesNotContain("1832", AgentContractHash.Content(WithRetrieval("v1", "1832 articles")), StringComparison.Ordinal);
        Assert.Equal(AgentContractHash.Of(WithRetrieval("v1")), AgentContractHash.Of(WithRetrieval("v2")));
        Assert.DoesNotContain("\"v1\"", AgentContractHash.Content(WithRetrieval("v1")), StringComparison.Ordinal);
    }

    private static AgentContract WithSystemTool(string description) =>
        Contract() with
        {
            Context = new ContextDefinition
            {
                Tools =
                [
                    new ToolDefinition
                    {
                        Name = "book_visit",
                        Description = description,
                        Parameters = JsonDocument.Parse("""{"type":"object"}""").RootElement,
                    },
                ],
            },
        };

    private static JsonElement ContextOf(string json) => JsonDocument.Parse(json).RootElement.GetProperty("context");

    [Fact]
    [Trait("criterion", "AC-system-tools-2")]
    public void A_context_tool_is_part_of_the_content_and_an_empty_list_is_not()
    {
        Assert.Equal(AgentContractHash.Of(WithSystemTool("Book a visit.")), AgentContractHash.Of(WithSystemTool("Book a visit.")));
        Assert.NotEqual(AgentContractHash.Of(WithSystemTool("Book a visit.")), AgentContractHash.Of(WithSystemTool("Book the visit.")));
        Assert.NotEqual(AgentContractHash.Of(Contract()), AgentContractHash.Of(WithSystemTool("Book a visit.")));
        Assert.False(ContextOf(AgentContractHash.Content(Contract())).TryGetProperty("tools", out _));
        Assert.False(ContextOf(JsonSerializer.Serialize(Contract(), ContractJson.Options)).TryGetProperty("tools", out _));
        Assert.Contains(
            "\"tools\":[{\"name\":\"book_visit\"",
            AgentContractHash.Content(WithSystemTool("Book a visit.")),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void The_hash_is_the_one_the_server_registers_the_content_under() =>
        Assert.Equal(ContentHash.Of(AgentContractHash.Content(Contract())), AgentContractHash.Of(Contract()));

    // Why: agents send this hash as their version name, so changing it splits every stored version; change the expected value only together with a migration
    [Fact]
    [Trait("criterion", "AC-system-tools-2")]
    public void The_hash_of_a_known_contract_does_not_change() =>
        Assert.Equal(
            "27109d34e75c78ccd2de5ec90629f05c239578d975419107d6a65df2e4e16e26",
            AgentContractHash.Of(WithRetrieval("v1", "1832 articles"))
        );
}
