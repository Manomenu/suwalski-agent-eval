using Microsoft.Extensions.AI;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Simulation;
using Suwageval.Server.Tests.Judges;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Simulation;

/// <summary>The simulated user against recorded answers of its model — the gate never calls a model.</summary>
public sealed class UserSimulatorTests
{
    private const string Anna = "Anna wants a refund for a broken kettle.";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly ConversationItem[] Conversation =
    [
        new UserMessage { Text = "My kettle arrived broken." },
        Call(
            "get_order", /*lang=json,strict*/
            """{"order_id":"A-17"}"""
        ),
        new ToolResult { CallId = "c1", Content = "secret tool output" },
        Emit("state.changed"),
        new AssistantMessage { Text = "Sorry to hear that. Shall I refund it?" },
    ];

    [Fact]
    public async Task A_message_from_the_model_is_the_users_next_message()
    {
        var turn = await RecordedModel.SimulatorAnswering("simulated-message.json").NextAsync(Anna, Conversation, Cancellation);

        Assert.Equal("It arrived broken, I want my money back.", turn.Message);
        Assert.Null(turn.Failure);
    }

    [Fact]
    public async Task End_from_the_model_ends_the_conversation()
    {
        var turn = await RecordedModel.SimulatorAnswering("simulated-end.json").NextAsync(Anna, Conversation, Cancellation);

        Assert.Null(turn.Message);
        Assert.Null(turn.Failure);
    }

    [Theory]
    [InlineData("garbage.txt")]
    [InlineData("simulated-no-message.json")]
    [InlineData("passed.json")]
    public async Task An_answer_that_is_not_the_json_asked_for_is_a_failure_never_an_exception(string file)
    {
        var turn = await RecordedModel.SimulatorAnswering(file).NextAsync(Anna, Conversation, Cancellation);

        Assert.Null(turn.Message);
        Assert.StartsWith("the simulator model's answer is not the JSON asked for", turn.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_model_that_cannot_be_asked_is_a_failure_never_an_exception()
    {
        var turn = await RecordedModel
            .SimulatorFailing(new HttpRequestException("connection refused"))
            .NextAsync(Anna, Conversation, Cancellation);

        Assert.Equal("the simulator model could not be asked (connection refused)", turn.Failure);
    }

    [Fact]
    public async Task The_model_is_asked_with_the_configured_temperature_and_the_users_description()
    {
        using var model = RecordedModel.Answering("simulated-message.json");

        await model.Simulator(temperature: 1.1).NextAsync(Anna, Conversation, Cancellation);

        var (messages, options) = Assert.Single(model.Requests);
        Assert.Equal(1.1f, options?.Temperature);
        Assert.Contains(Anna, string.Join('\n', messages.Select(message => message.Text)), StringComparison.Ordinal);
        Assert.NotNull(options?.ResponseFormat as ChatResponseFormatJson);
    }

    [Fact]
    public void The_user_sees_only_the_messages_never_the_tools_events_or_their_output()
    {
        var prompt = UserSimulator.Prompt(Anna, Conversation);

        Assert.Contains("User: My kettle arrived broken.", prompt, StringComparison.Ordinal);
        Assert.Contains("Agent: Sorry to hear that. Shall I refund it?", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("get_order", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("secret tool output", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("state.changed", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_turn_in_which_the_agent_wrote_nothing_is_shown_as_such()
    {
        var prompt = UserSimulator.Prompt(
            Anna,
            [
                new UserMessage { Text = "Refund it." },
                Call(
                    "refund_order", /*lang=json,strict*/
                    """{"order_id":"A-17"}"""
                ),
                new UserMessage { Text = "Hello?" },
            ]
        );

        Assert.Contains(
            "User: Refund it.\nAgent: (the agent wrote nothing)\nUser: Hello?\nAgent: (the agent wrote nothing)",
            prompt.ReplaceLineEndings("\n"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_empty_conversation_tells_the_user_to_write_first() =>
        Assert.Contains("the user writes first", UserSimulator.Prompt(Anna, []), StringComparison.Ordinal);
}
