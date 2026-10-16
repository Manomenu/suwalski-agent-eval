using System.Text.Json;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Tests.Agents;

public sealed class ContractContentTests
{
    private static AgentContract Contract(string prompt = "Be helpful.") =>
        new()
        {
            SystemPrompt = prompt,
            Model = new ModelDefinition { Name = "some-model" },
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
    public void An_empty_prompt_model_and_repeated_names_are_problems()
    {
        var tool = new ToolDefinition
        {
            Name = "t",
            Description = "d",
            Parameters = JsonDocument.Parse("{}").RootElement,
        };
        var @event = new EventDeclaration { Type = "e", DataSchema = tool.Parameters };

        var problems = ContractContent.Problems(
            new AgentContract
            {
                SystemPrompt = " ",
                Model = new ModelDefinition { Name = "" },
                Tools = [tool, tool],
                Context = new ContextDefinition { Events = [@event, @event] },
            }
        );

        Assert.Equal(4, problems.Count);
        Assert.StartsWith("systemPrompt", problems[0], StringComparison.Ordinal);
        Assert.StartsWith("model.name", problems[1], StringComparison.Ordinal);
        Assert.StartsWith("tools[1].name", problems[2], StringComparison.Ordinal);
        Assert.StartsWith("context.events[1].type", problems[3], StringComparison.Ordinal);
    }
}
