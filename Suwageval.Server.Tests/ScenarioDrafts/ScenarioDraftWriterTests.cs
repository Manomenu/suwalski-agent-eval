using System.Text.Json;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Evaluation.Simulation;
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

    private static readonly CheckInput NoRefund = new ToolNotCalledCheck
    {
        Key = "no-refund",
        Tool = "refund_order",
        Description = "Never refunds on its own.",
    };

    private static List<ConversationItem> Items() => [.. Turns().SelectMany(turn => turn)];

    [Fact]
    public void The_whole_conversation_is_copied_to_its_last_message_and_remembers_where_it_came_from()
    {
        var draft = ScenarioDraftWriter.Draft("Chat #81", Id, Items(), upTo: null, []);

        Assert.Equal("conversation-chat-81", draft.Key);
        Assert.Equal("suwageval/scenarios/conversation-chat-81.yaml", draft.Path);
        Assert.Contains("copied to its end", draft.Yaml, StringComparison.Ordinal);
        Assert.Contains("- user: Hi\n- assistant: Hello, how can I help?\n- user: Refund order A-17", draft.Yaml, StringComparison.Ordinal);
        // The last answer cites a source, so it is written in full rather than as the shorthand.
        Assert.Contains("text: Refunded.", draft.Yaml, StringComparison.Ordinal);
        Assert.Contains($"origin:\n  monitoringConversation: {Id}\n", draft.Yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("checks", draft.Yaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, 1, "- user: Hi\n", "- assistant: Hello")]
    [InlineData(false, 1, "- assistant: Hello, how can I help?\n", "- user: Refund")]
    [InlineData(true, 2, "- user: Refund order A-17, it arrived broken.\n", "refund_order")]
    public void A_copy_up_to_a_message_ends_with_it(bool userMessage, int ordinal, string last, string after)
    {
        var draft = ScenarioDraftWriter.Draft("chat-81", Id, Items(), new MessageCut(userMessage, ordinal), []);

        Assert.Contains($"{last}origin:", draft.Yaml, StringComparison.Ordinal);
        Assert.DoesNotContain(after, draft.Yaml, StringComparison.Ordinal);
        Assert.Contains($"up to {(userMessage ? "user" : "agent")} message {ordinal}, included", draft.Yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void The_failed_monitoring_checks_come_below_as_comments_with_their_definitions()
    {
        var yaml = ScenarioDraftWriter.Draft("chat-81", Id, Items(), upTo: null, [NoRefund]).Yaml;

        Assert.Contains("# checks:\n", yaml, StringComparison.Ordinal);
        Assert.Contains("key: no-refund\n", yaml, StringComparison.Ordinal);
        Assert.Contains("type: tool-not-called\n", yaml, StringComparison.Ordinal);
        Assert.Contains("#   tool: refund_order\n", yaml, StringComparison.Ordinal);
        Assert.Contains("#   description: Never refunds on its own.\n", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("\nchecks:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_message_the_conversation_does_not_have_is_no_cut()
    {
        Assert.Null(new MessageCut(true, 3).IndexIn(Items()));
        Assert.Equal(1, new MessageCut(false, 1).IndexIn(Items()));
    }

    [Fact]
    public void A_conversation_id_without_letters_or_digits_gives_a_key_from_suwagevals_id_and_a_long_one_is_cut()
    {
        Assert.Equal("conversation-cccccccc", ScenarioDraftWriter.Key("###", Id));
        var key = ScenarioDraftWriter.Key(new string('a', 100), Id);
        Assert.Equal(ScenarioDraftWriter.MaxKeyLength, key.Length);
        Assert.DoesNotContain("--", ScenarioDraftWriter.Key("a__b", Id), StringComparison.Ordinal);
    }

    [Fact]
    public void With_the_rest_simulated_the_draft_continues_with_the_described_user_for_the_turns_asked()
    {
        var draft = ScenarioDraftWriter.Draft(
            "chat-81",
            Id,
            Items(),
            new MessageCut(true, 1),
            [],
            (new UserDescription("You are a customer whose kettle broke."), 2)
        );

        Assert.Contains("copied up to user message 1, included; the rest simulated.", draft.Yaml, StringComparison.Ordinal);
        Assert.Contains("described by a model from the whole conversation: review it", draft.Yaml, StringComparison.Ordinal);
        Assert.Contains(
            "continuation:\n  turns: 2\n  simulatedUser: You are a customer whose kettle broke.\n",
            draft.Yaml,
            StringComparison.Ordinal
        );
        Assert.Null(draft.Notice);
    }

    [Fact]
    public void A_description_no_model_wrote_is_said_in_the_file_and_in_the_notice()
    {
        var draft = ScenarioDraftWriter.Draft(
            "chat-81",
            Id,
            Items(),
            new MessageCut(true, 1),
            [],
            (new UserDescription("Rewrite: …", "The simulated user was not described by a model: no model."), 2)
        );

        Assert.Contains("# The simulated user is not described yet", draft.Yaml, StringComparison.Ordinal);
        Assert.Equal("The simulated user was not described by a model: no model.", draft.Notice);
    }
}
