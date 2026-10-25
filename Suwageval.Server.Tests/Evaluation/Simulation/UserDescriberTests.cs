using System.Diagnostics.CodeAnalysis;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.LanguageModels;
using Suwageval.Server.Tests.Judges;
using static Suwageval.Server.Tests.Evaluation.Checks.Items;

namespace Suwageval.Server.Tests.Evaluation.Simulation;

/// <summary>The simulated user's description drafted from a real conversation, against recorded answers of its model.</summary>
[SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "The recorded model and the clients over it hold nothing but memory."
)]
public sealed class UserDescriberTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly ConversationItem[] Conversation =
    [
        new UserMessage { Text = "My order A-17 arrived broken." },
        Call(
            "get_order", /*lang=json,strict*/
            """{"order_id":"A-17"}"""
        ),
        new ToolResult { CallId = "c1", Content = "secret tool output" },
        new AssistantMessage { Text = "Sorry. Shall I repair it?" },
        new UserMessage { Text = "No, I want my money back." },
    ];

    [Fact]
    public async Task The_model_describes_the_user_from_what_the_user_saw_through_the_fallback()
    {
        var model = RecordedModel.Answering("described-user.json");
        var describer = model.Describer();

        var description = await describer.DescribeAsync(Conversation, Cancellation);

        Assert.StartsWith("You are a customer whose order A-17 arrived broken.", description.Text, StringComparison.Ordinal);
        Assert.Null(description.Notice);
        Assert.Equal(("fallback", false), (describer.ModelEntry, describer.HasOwnModel));
        var prompt = Assert.Single(model.Requests).Messages[^1].Text;
        Assert.Contains(
            "User: My order A-17 arrived broken.\nAgent: Sorry. Shall I repair it?\nUser: No, I want",
            prompt,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("secret tool output", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_model_the_draft_lists_the_users_messages_and_says_why()
    {
        var describer = new UserDescriber(new ModelClients([], _ => throw new InvalidOperationException("no model")));

        var description = await describer.DescribeAsync(Conversation, Cancellation);

        Assert.Contains("- My order A-17 arrived broken.\n- No, I want my money back.", description.Text, StringComparison.Ordinal);
        Assert.Equal("The simulated user was not described by a model: no describer or fallback model is configured.", description.Notice);
    }

    [Fact]
    public async Task An_answer_that_is_not_the_json_asked_for_leaves_the_plain_draft()
    {
        var description = await RecordedModel.Answering("simulated-message.json").Describer().DescribeAsync(Conversation, Cancellation);

        Assert.StartsWith("Rewrite:", description.Text, StringComparison.Ordinal);
        Assert.Contains("not the JSON asked for", description.Notice, StringComparison.Ordinal);
    }
}
