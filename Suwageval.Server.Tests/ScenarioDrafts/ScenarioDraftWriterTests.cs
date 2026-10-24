using System.Text.Json;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.ScenarioDrafts;

namespace Suwageval.Server.Tests.ScenarioDrafts;

public sealed class ScenarioDraftWriterTests
{
    private static readonly Guid Id = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    internal static IReadOnlyList<IReadOnlyList<ConversationItem>> Turns() =>
        [
            [new UserMessage { Text = "Hi" }, new AssistantMessage { Text = "Hello, how can I help?" }],
            [
                new UserMessage { Text = "Refund order A-17, it arrived broken." },
                new ToolCall
                {
                    Id = "c1",
                    Name = "refund_order",
                    Arguments = JsonSerializer.SerializeToElement(new { order_id = "A-17", reason = "broken" }),
                    Agent = "support",
                },
                new ToolResult { CallId = "c1", Content = """{"refunded":89.9}""" },
                new Event { Type = "state.changed", Data = JsonSerializer.SerializeToElement(new { node = "refunded", orderId = "A-17" }) },
                new AssistantMessage { Text = "Refunded.", Sources = ["refunds"] },
            ],
        ];

    [Fact]
    public void The_history_is_every_turn_before_the_last_and_the_scenario_ends_with_the_last_user_message()
    {
        var draft = ScenarioDraftWriter.Draft("Chat #81", Id, Turns());

        Assert.Equal("conversation-chat-81", draft.Key);
        Assert.Equal("suwageval/scenarios/conversation-chat-81.yaml", draft.Path);
        var yaml = draft.Yaml;
        Assert.Contains(
            "- user: Hi\n- assistant: Hello, how can I help?\n- user: Refund order A-17, it arrived broken.\n",
            yaml,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("\n- assistant: Refunded.", yaml, StringComparison.Ordinal);
        Assert.Contains($"origin:\n  monitoringConversation: {Id}\n", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void What_the_agent_did_in_the_last_turn_is_drafted_as_commented_checks()
    {
        var yaml = ScenarioDraftWriter.Draft("chat-81", Id, Turns()).Yaml;

        Assert.Contains("# checks:\n", yaml, StringComparison.Ordinal);
        Assert.Contains(
            "# - key: calls-refund-order\n#   type: tool-called\n#   tool: refund_order\n#   by: support\n",
            yaml,
            StringComparison.Ordinal
        );
        Assert.Contains("# - key: emits-state-changed\n#   type: event\n", yaml, StringComparison.Ordinal);
        Assert.Contains("# - key: cites-its-sources\n#   type: cited\n", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("\nchecks:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_conversation_id_without_letters_or_digits_gives_a_key_from_suwagevals_id_and_a_long_one_is_cut()
    {
        Assert.Equal("conversation-cccccccc", ScenarioDraftWriter.Key("###", Id));
        var key = ScenarioDraftWriter.Key(new string('a', 100), Id);
        Assert.Equal(ScenarioDraftWriter.MaxKeyLength, key.Length);
        Assert.DoesNotContain("--", ScenarioDraftWriter.Key("a__b", Id), StringComparison.Ordinal);
    }
}
