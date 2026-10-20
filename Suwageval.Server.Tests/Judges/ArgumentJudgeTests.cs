using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Judges;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Judges;

/// <summary>
/// <c>$judge</c> matching against recorded answers of the model (<c>Answers/argument*.json</c>: real ones captured from the model,
/// and malformed ones) — the gate never calls a model.
/// </summary>
[SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "The recorded model and the clients over it hold nothing but memory."
)]
public sealed class ArgumentJudgeTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly ToolCalledCheck Reason = new()
    {
        Key = "reason",
        ScenarioKey = "refund",
        Tool = "refund_order",
        Args = Json(
            /*lang=json,strict*/
            """
            {"order_id":"A-17","reason":{"$judge":{"description":"Says the item arrived damaged or broken","examples":["kettle arrived broken","damaged on delivery"]}}}
            """
        ),
    };

    private static readonly ToolCalledCheck Message = new()
    {
        Key = "message",
        ScenarioKey = "refund",
        Tool = "send_message",
        Args = Json(
            /*lang=json,strict*/
            """
            {"to":"c-1","subject":{"$judge":{"description":"A short, polite subject line about the order"}},"body":{"$judge":{"description":"Apologises to the customer for the delay","examples":["We are sorry your parcel is late."]}}}
            """
        ),
    };

    private static ToolCall Refund(string reason, string order = "A-17") =>
        Call("refund_order", $$"""{"order_id":"{{order}}","reason":"{{reason}}"}""", $"call-{order}-{reason.Length}");

    private static readonly ToolCall Letter = Call(
        "send_message",
        /*lang=json,strict*/
        """{"to":"c-1","subject":"Your order A-17","body":"Your parcel is on its way, thanks for nothing."}"""
    );

    private static string Text((IReadOnlyList<ChatMessage> Messages, ChatOptions? Options) request) =>
        string.Join('\n', request.Messages.Select(message => message.Text));

    [Fact]
    public async Task When_every_judged_value_is_correct_the_check_passes_with_the_reason_and_that_call_as_evidence()
    {
        var model = RecordedModel.Answering("argument-correct.json");
        var call = Refund("The kettle was cracked when I opened the box.");

        var verdict = await model.ArgumentJudge().JudgeAsync(Reason, Turn(Say("ok"), call), Cancellation);

        Assert.True(verdict.Passed);
        Assert.False(verdict.NotJudged);
        Assert.Equal([call], verdict.Evidence);
        Assert.StartsWith("refund_order was called, and its judged values fit.", verdict.Message, StringComparison.Ordinal);
        Assert.Contains("args.reason: The statement says the kettle was cracked", verdict.Message, StringComparison.Ordinal);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task An_incorrect_value_fails_the_check_and_the_message_names_the_path_and_the_models_reason()
    {
        var model = RecordedModel.Answering("argument-incorrect.json");
        var call = Refund("I changed my mind and want my money back.");

        var verdict = await model.ArgumentJudge().JudgeAsync(Reason, Turn(call), Cancellation);

        Assert.False(verdict.Passed);
        Assert.False(verdict.NotJudged);
        Assert.Equal([call], verdict.Evidence);
        Assert.Contains(
            "args.reason does not fit: The reason says the customer changed their mind",
            verdict.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task One_request_covers_all_the_judged_values_of_a_call_and_one_incorrect_one_fails_it()
    {
        var model = RecordedModel.Answering("arguments-one-incorrect.json");

        var verdict = await model.ArgumentJudge().JudgeAsync(Message, Turn(Letter), Cancellation);

        Assert.False(verdict.Passed);
        Assert.False(verdict.NotJudged);
        Assert.Contains("args.body does not fit: It does not apologize", verdict.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("args.subject does not fit", verdict.Message, StringComparison.Ordinal);
        var prompt = Text(Assert.Single(model.Requests));
        Assert.Contains("\"path\":\"args.subject\"", prompt, StringComparison.Ordinal);
        Assert.Contains("\"path\":\"args.body\"", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("argument-garbage.txt")]
    [InlineData("argument-wrong-type.json")]
    [InlineData("arguments-missing-path.json")]
    [InlineData("arguments-extra-path.json")]
    [InlineData("arguments-duplicate-path.json")]
    public async Task An_answer_that_is_not_one_entry_per_path_is_not_judged(string answer)
    {
        var judge = RecordedModel.Answering(answer).ArgumentJudge();

        var verdict = await judge.JudgeAsync(Message, Turn(Letter), Cancellation);

        Assert.False(verdict.Passed);
        Assert.True(verdict.NotJudged);
        Assert.StartsWith("Not judged: the judge model's answer is not the JSON asked for", verdict.Message, StringComparison.Ordinal);
        Assert.Equal([Letter], verdict.Evidence);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task A_model_that_cannot_be_asked_is_not_judged_and_nothing_is_thrown(Type error)
    {
        var judge = RecordedModel.Failing((Exception)Activator.CreateInstance(error, "boom")!).ArgumentJudge();

        var verdict = await judge.JudgeAsync(Reason, Turn(Refund("broken")), Cancellation);

        Assert.False(verdict.Passed);
        Assert.True(verdict.NotJudged);
        Assert.StartsWith("Not judged: the judge model could not be asked (", verdict.Message, StringComparison.Ordinal);
        Assert.Contains("boom", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_judge_model_the_check_is_not_judged_and_nothing_is_asked()
    {
        var model = RecordedModel.Answering("argument-correct.json");
        var judge = new ArgumentJudge(model.Clients(), Options.Create(new JudgeOptions { Model = " " }));

        var verdict = await judge.JudgeAsync(Reason, Turn(Refund("broken")), Cancellation);

        Assert.True(verdict.NotJudged);
        Assert.Contains("no judge model is configured", verdict.Message, StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Cancelling_stops_the_judge_instead_of_calling_it_not_judged()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var model = new RecordedModel(async cancellation =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return "";
        });
        var judging = model.ArgumentJudge().JudgeAsync(Reason, Turn(Refund("broken")), source.Token);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => judging);
    }

    [Fact]
    public async Task A_call_that_fails_the_literals_is_not_sent_to_the_model_and_the_next_one_is()
    {
        var model = RecordedModel.Answering("argument-correct.json");
        var wrongOrder = Refund("The kettle was cracked.", order: "B-2");
        var right = Refund("The kettle was cracked when I opened the box.");

        var verdict = await model.ArgumentJudge().JudgeAsync(Reason, Turn(wrongOrder, right), Cancellation);

        Assert.True(verdict.Passed);
        Assert.Equal([right], verdict.Evidence);
        var prompt = Text(Assert.Single(model.Requests));
        Assert.Contains("cracked when I opened the box", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("B-2", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_call_without_the_judged_path_fails_without_asking_the_model()
    {
        var model = RecordedModel.Answering("argument-correct.json");
        var call = Call(
            "refund_order", /*lang=json,strict*/
            """{"order_id":"A-17"}"""
        );

        var verdict = await model.ArgumentJudge().JudgeAsync(Reason, Turn(call), Cancellation);

        Assert.False(verdict.Passed);
        Assert.False(verdict.NotJudged);
        Assert.Equal([call], verdict.Evidence);
        Assert.Contains("but not with arguments containing", verdict.Message, StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task A_tool_that_was_not_called_fails_without_asking_the_model()
    {
        var model = RecordedModel.Answering("argument-correct.json");

        var verdict = await model.ArgumentJudge().JudgeAsync(Reason, Turn(Say("hi")), Cancellation);

        Assert.False(verdict.Passed);
        Assert.False(verdict.NotJudged);
        Assert.Equal("refund_order was not called.", verdict.Message);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task The_first_incorrect_candidate_does_not_stop_the_search_and_the_first_matching_one_does()
    {
        var model = RecordedModel.Sequence("argument-incorrect.json", "argument-correct.json", "argument-incorrect.json");
        var first = Refund("I changed my mind and want my money back.");
        var second = Refund("The kettle was cracked when I opened the box.");
        var third = Refund("Third call, never judged.");

        var verdict = await model.ArgumentJudge().JudgeAsync(Reason, Turn(first, second, third), Cancellation);

        Assert.True(verdict.Passed);
        Assert.Equal([second], verdict.Evidence);
        Assert.Equal(2, model.Requests.Count);
    }

    [Fact]
    public async Task When_no_candidate_fits_every_one_is_numbered_in_the_message_and_all_are_the_evidence()
    {
        var model = RecordedModel.Answering("argument-incorrect.json");
        var first = Refund("I changed my mind.");
        var second = Refund("Too expensive.");

        var verdict = await model.ArgumentJudge().JudgeAsync(Reason, Turn(first, second), Cancellation);

        Assert.False(verdict.Passed);
        Assert.Equal([first, second], verdict.Evidence);
        Assert.Contains("[1] args.reason does not fit", verdict.Message, StringComparison.Ordinal);
        Assert.Contains("[2] args.reason does not fit", verdict.Message, StringComparison.Ordinal);
        Assert.Equal(2, model.Requests.Count);
    }

    [Fact]
    public async Task A_candidate_that_cannot_be_judged_makes_the_whole_check_not_judged_even_after_an_incorrect_one()
    {
        var model = RecordedModel.Sequence("argument-incorrect.json", "argument-garbage.txt");

        var verdict = await model.ArgumentJudge().JudgeAsync(Reason, Turn(Refund("one"), Refund("two")), Cancellation);

        Assert.True(verdict.NotJudged);
        Assert.False(verdict.Passed);
    }

    [Fact]
    public async Task An_event_check_judges_its_data()
    {
        var model = new RecordedModel(_ =>
            Task.FromResult( /*lang=json,strict*/
                """{"results":[{"path":"data.reason","correct":true,"reason":"A cracked kettle is damaged."}]}"""
            )
        );
        var check = new EventCheck
        {
            Key = "note",
            ScenarioKey = "refund",
            Event = "state.changed",
            Data = Json( /*lang=json,strict*/
                """{"node":"refunded","reason":{"$judge":{"description":"Says the kettle was damaged"}}}"""
            ),
        };
        var emitted = Emit(
            "state.changed", /*lang=json,strict*/
            """{"node":"refunded","reason":"cracked kettle"}"""
        );

        var verdict = await model.ArgumentJudge().JudgeAsync(check, Turn(emitted), Cancellation);

        Assert.True(verdict.Passed);
        Assert.StartsWith("Event state.changed was emitted, and its judged values fit.", verdict.Message, StringComparison.Ordinal);
        Assert.Equal([emitted], verdict.Evidence);
        Assert.Contains("event state.changed", Text(Assert.Single(model.Requests)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_model_gets_the_values_the_descriptions_and_the_examples_at_temperature_zero_asking_for_json()
    {
        var model = RecordedModel.Answering("argument-correct.json");

        await model.ArgumentJudge().JudgeAsync(Reason, Turn(Refund("The kettle was cracked when I opened the box.")), Cancellation);

        var (messages, options) = Assert.Single(model.Requests);
        var text = string.Join('\n', messages.Select(message => message.Text));
        Assert.Contains("\"context\":\"tool call refund_order\"", text, StringComparison.Ordinal);
        Assert.Contains("\"path\":\"args.reason\"", text, StringComparison.Ordinal);
        Assert.Contains("\"actual\":\"The kettle was cracked when I opened the box.\"", text, StringComparison.Ordinal);
        Assert.Contains("\"description\":\"Says the item arrived damaged or broken\"", text, StringComparison.Ordinal);
        Assert.Contains("\"examples\":[\"kettle arrived broken\",\"damaged on delivery\"]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("A-17", text, StringComparison.Ordinal);
        Assert.Equal(0f, options?.Temperature);
        Assert.NotNull(((ChatResponseFormatJson?)options?.ResponseFormat)?.Schema);
    }

    [Fact]
    public async Task Only_what_the_matcher_gives_is_in_the_prompt_and_examples_may_be_any_json()
    {
        var model = RecordedModel.Answering("argument-correct.json");
        var check = Reason with
        {
            Args = Json( /*lang=json,strict*/
                """{"reason":{"$judge":{"examples":[{"code":1},["a"],7]}}}"""
            ),
        };

        await model.ArgumentJudge().JudgeAsync(check, Turn(Refund("x")), Cancellation);

        var text = Text(Assert.Single(model.Requests));
        Assert.DoesNotContain("description", text, StringComparison.Ordinal);
        Assert.Contains("\"examples\":[{\"code\":1},[\"a\"],7]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_check_without_a_matcher_is_refused()
    {
        var plain = Reason with
        {
            Args = Json( /*lang=json,strict*/
                """{"order_id":"A-17"}"""
            ),
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            RecordedModel.Answering("argument-correct.json").ArgumentJudge().JudgeAsync(plain, Turn(Refund("x")), Cancellation)
        );
    }
}
