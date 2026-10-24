using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Evaluation.Runs;

namespace Suwageval.Server.Tests.Evaluation.Runs;

public sealed class UndeclaredOutputTests
{
    private static ToolCall Call(string name, string? agent = null) =>
        new()
        {
            Id = "c",
            Name = name,
            Agent = agent,
            Arguments = System.Text.Json.JsonDocument.Parse("{}").RootElement,
        };

    private static Event Emitted(string type) => new() { Type = type, Data = System.Text.Json.JsonDocument.Parse("{}").RootElement };

    [Fact]
    public void Declared_tools_and_events_and_plain_text_are_not_reported() =>
        Assert.Empty(
            UndeclaredOutput.Of(
                [Call("get_order"), Emitted("state.changed"), new AssistantMessage { Text = "hi" }],
                SeededAgent.ExampleVersion
            )
        );

    [Fact]
    public void A_tool_or_an_event_the_version_does_not_declare_is_named_once_in_the_order_it_appears() =>
        Assert.Equal(
            ["tool 'cancel_order'", "event 'order.cancelled'"],
            UndeclaredOutput.Of(
                [Call("cancel_order"), Emitted("order.cancelled"), Call("cancel_order"), Call("get_order")],
                SeededAgent.ExampleVersion
            )
        );

    [Fact]
    public void A_call_that_names_an_agent_is_checked_against_that_agents_tools() =>
        Assert.Equal(
            ["agent 'ghost'", "tool 'cancel_order' of agent 'support-agent'"],
            UndeclaredOutput.Of(
                [
                    Call("get_order", "support-agent"),
                    Call("get_order", "ghost"),
                    Call("cancel_order", "support-agent"),
                    Call("get_order", "ghost"),
                ],
                SeededAgent.ExampleVersion
            )
        );

    [Fact]
    public void A_tool_only_another_agent_has_is_undeclared_for_the_agent_that_called_it()
    {
        var version = SeededAgent.ExampleVersion with
        {
            Agents = [SeededAgent.ExampleVersion.Agents[0], SeededAgent.ExampleVersion.Agents[0] with { Name = "billing", Tools = [] }],
        };

        Assert.Equal(["tool 'get_order' of agent 'billing'"], UndeclaredOutput.Of([Call("get_order", "billing")], version));
        Assert.Empty(UndeclaredOutput.Of([Call("get_order")], version));
    }
}
