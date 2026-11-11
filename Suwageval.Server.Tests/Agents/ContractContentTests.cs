using Suwageval.AgentSdk.Agents;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Tests.Agents;

public sealed class ContractContentTests
{
    private static LlmAgentDefinition Agent(string name, string config = "c", string model = "some-model", params ToolDefinition[] tools) =>
        new()
        {
            Name = name,
            Model = new ModelDefinition { Name = model },
            Config = config,
            Tools = tools,
        };

    [Fact]
    public void The_model_names_are_those_of_all_agents_without_repeats_in_the_agents_order() =>
        Assert.Equal(
            ["large", "small"],
            ContractContent.ModelNames(
                new AgentContract { Agents = [Agent("a", model: "large"), Agent("b", model: "small"), Agent("c", model: "large")] }
            )
        );

    [Fact]
    [Trait("criterion", "AC-system-tools-6")]
    public void A_stored_version_from_before_context_tools_reads_as_one_without_them() =>
        Assert.Empty(
            ContractContent
                .Read(
                    """
                    {"label":null,"agents":[{"name":"a","model":{"name":"m","parameters":null},"config":"c","tools":[]}],"otherConfig":null,
                     "context":{"usesRetrieval":false,"retrieval":null,"events":[],"fixturesSchema":null}}
                    """
                )
                .Context.Tools
        );
}
