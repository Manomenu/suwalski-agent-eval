using Suwageval.AgentSdk.Agents;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Tests.Agents;

public sealed class ContractYamlTests
{
    [Fact]
    public void A_contract_is_written_in_its_key_order_without_unset_fields_and_a_prompt_as_a_block()
    {
        var contract = new AgentContract
        {
            Label = "v1",
            Agents =
            [
                new LlmAgentDefinition
                {
                    Name = "main",
                    Model = new ModelDefinition { Name = "gpt-4o" },
                    Config = "Line one.\nLine two.\n",
                },
            ],
        };

        var yaml = ContractYaml.Of(contract);

        Assert.StartsWith("label: v1\nagents:\n", yaml, StringComparison.Ordinal);
        Assert.Contains("  config: |\n    Line one.\n    Line two.\n", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("otherConfig", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', yaml);
        Assert.EndsWith("events: []\n", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_text_a_reader_would_take_for_a_number_or_a_boolean_stays_a_string()
    {
        var contract = new AgentContract
        {
            Label = "1.0",
            Agents =
            [
                new LlmAgentDefinition
                {
                    Name = "true",
                    Model = new ModelDefinition { Name = "m" },
                    Config = "",
                },
            ],
        };

        var yaml = ContractYaml.Of(contract);

        Assert.Contains("label: \"1.0\"", yaml, StringComparison.Ordinal);
        Assert.Contains("name: \"true\"", yaml, StringComparison.Ordinal);
        Assert.Contains("config: \"\"", yaml, StringComparison.Ordinal);
    }
}
