using System.Text.Json.Nodes;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Tests.Files;

public sealed class ConversationShorthandsTests
{
    private static (JsonNode? Expanded, List<Problem> Problems) Expand(string yamlItem)
    {
        var problems = new List<Problem>();
        var source = YamlSource.Read("s.yaml", $"conversation:\n  - {yamlItem}\n", problems)!;
        var item = ((JsonArray)((JsonObject)source.Root!)["conversation"]!)[0];
        return (ConversationShorthands.Expand(item, "conversation[0]", source, problems), problems);
    }

    [Theory]
    [InlineData(
        "user: Hello", /*lang=json,strict*/
        """{"kind":"UserMessage","text":"Hello"}"""
    )]
    [InlineData(
        "assistant: Hi there", /*lang=json,strict*/
        """{"kind":"AssistantMessage","text":"Hi there"}"""
    )]
    [InlineData(
        "tool_call: { id: c1, name: get_order, arguments: { order_id: A-17 } }",
        /*lang=json,strict*/
        """{"kind":"ToolCall","id":"c1","name":"get_order","arguments":{"order_id":"A-17"}}"""
    )]
    [InlineData(
        "tool_call: { id: c1, name: get_order, arguments: {}, agent: support-agent }",
        /*lang=json,strict*/
        """{"kind":"ToolCall","id":"c1","name":"get_order","arguments":{},"agent":"support-agent"}"""
    )]
    [InlineData(
        "tool_result: { call_id: c1, content: ok, isError: true }",
        /*lang=json,strict*/
        """{"kind":"ToolResult","callId":"c1","content":"ok","isError":true}"""
    )]
    [InlineData(
        "event: { type: state.changed, data: { n: 1 } }", /*lang=json,strict*/
        """{"kind":"Event","type":"state.changed","data":{"n":1}}"""
    )]
    public void A_shorthand_becomes_the_full_form(string yamlItem, string expectedJson)
    {
        var (expanded, problems) = Expand(yamlItem);

        Assert.Empty(problems);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expectedJson), expanded), expanded?.ToJsonString());
    }

    [Fact]
    public void The_full_form_passes_through()
    {
        var (expanded, problems) = Expand("{ kind: UserMessage, text: Hello }");

        Assert.Empty(problems);
        Assert.Equal( /*lang=json,strict*/
            """{"kind":"UserMessage","text":"Hello"}""",
            expanded?.ToJsonString()
        );
    }

    [Theory]
    [InlineData("shout: Hello")]
    [InlineData("{ user: Hello, assistant: Hi }")]
    [InlineData("just text")]
    public void An_item_that_is_neither_form_is_a_problem(string yamlItem)
    {
        var (expanded, problems) = Expand(yamlItem);

        Assert.Null(expanded);
        Assert.Equal("conversation[0]", Assert.Single(problems).Path);
    }

    [Theory]
    [InlineData("user: { text: x }")]
    [InlineData("tool_call: plain")]
    public void A_shorthand_with_the_wrong_kind_of_value_is_a_problem(string yamlItem)
    {
        var (expanded, problems) = Expand(yamlItem);

        Assert.Null(expanded);
        Assert.Single(problems);
    }
}
