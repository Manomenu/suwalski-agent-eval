using Suwageval.Contracts.Conversations;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class UndeclaredOutputTests
{
    private static ToolCall Call(string name) =>
        new()
        {
            Id = "c",
            Name = name,
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
}
