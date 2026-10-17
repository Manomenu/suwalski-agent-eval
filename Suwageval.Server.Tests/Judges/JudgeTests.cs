using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Server.Checks;
using Suwageval.Server.Judges;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Judges;

/// <summary>The judge against recorded answers of the model (<c>Answers/</c>): real ones captured from qwen38, and malformed ones.</summary>
[SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "The recorded model and the clients over it hold nothing but memory."
)]
public sealed class JudgeTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly JudgeCheck PoliteRefusal = new()
    {
        Key = "polite-refusal",
        ScenarioKey = "refund",
        Criteria = "The assistant refuses to refund an order older than 30 days and explains why, politely.",
    };

    private static readonly UserMessage[] Conversation = [new() { Text = "Please refund order A-17, I bought it 60 days ago." }];

    private static readonly ConversationItem[] Output = [Call("get_order"), Say("I am sorry, but order A-17 is too old to refund.")];

    private static Task<CheckVerdict> JudgeAsync(RecordedModel model, JudgeCheck? check = null, ConversationItem[]? output = null) =>
        model.Judge().JudgeAsync(check ?? PoliteRefusal, Conversation, Turn(output ?? Output), Cancellation);

    [Fact]
    public async Task A_recorded_passing_answer_passes_and_its_reason_is_the_message()
    {
        var verdict = await JudgeAsync(RecordedModel.Answering("passed.json"));

        Assert.True(verdict.Passed);
        Assert.False(verdict.NotJudged);
        Assert.Contains("politely refuses", verdict.Message, StringComparison.Ordinal);
        Assert.Equal(Output, verdict.Evidence);
    }

    [Fact]
    public async Task A_recorded_failing_answer_fails_but_is_judged()
    {
        var verdict = await JudgeAsync(RecordedModel.Answering("failed.json"));

        Assert.False(verdict.Passed);
        Assert.False(verdict.NotJudged);
        Assert.Contains("did not refuse", verdict.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("garbage.txt")]
    [InlineData("missing-reason.json")]
    [InlineData("missing-passed.json")]
    [InlineData("wrong-type.json")]
    public async Task An_answer_that_is_not_the_json_asked_for_is_not_judged(string answer)
    {
        var verdict = await JudgeAsync(RecordedModel.Answering(answer));

        Assert.False(verdict.Passed);
        Assert.True(verdict.NotJudged);
        Assert.StartsWith("Not judged: the judge model's answer is not the JSON asked for", verdict.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task A_model_that_cannot_be_asked_is_not_judged_and_nothing_is_thrown(Type error)
    {
        var verdict = await JudgeAsync(RecordedModel.Failing((Exception)Activator.CreateInstance(error, "boom")!));

        Assert.False(verdict.Passed);
        Assert.True(verdict.NotJudged);
        Assert.Contains("could not be asked (", verdict.Message, StringComparison.Ordinal);
        Assert.Contains("boom", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_judge_model_the_check_is_not_judged()
    {
        var model = RecordedModel.Answering("passed.json");
        var judge = new Judge(model.Clients(), Options.Create(new JudgeOptions { Model = " " }));

        var verdict = await judge.JudgeAsync(PoliteRefusal, Conversation, Turn(Output), Cancellation);

        Assert.False(judge.IsConfigured);
        Assert.True(verdict.NotJudged);
        Assert.Empty(model.Requests);
        Assert.Null(judge.ModelName);
        Assert.Null(verdict.ModelName);
    }

    [Fact]
    public async Task The_verdict_names_the_model_that_was_asked()
    {
        var judge = RecordedModel.JudgeAnswering("passed.json");

        var verdict = await judge.JudgeAsync(PoliteRefusal, Conversation, Turn(Output), Cancellation);

        Assert.Equal("recorded", judge.ModelName);
        Assert.Equal("recorded", verdict.ModelName);
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
        var judging = model.Judge().JudgeAsync(PoliteRefusal, Conversation, Turn(Output), source.Token);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => judging);
    }

    [Fact]
    public async Task The_model_gets_the_criteria_the_conversation_and_the_output_at_temperature_zero_asking_for_json()
    {
        var model = RecordedModel.Answering("passed.json");

        await JudgeAsync(model);

        var (messages, options) = Assert.Single(model.Requests);
        var text = string.Join('\n', messages.Select(message => message.Text));
        Assert.Contains(PoliteRefusal.Criteria, text, StringComparison.Ordinal);
        Assert.Contains("Please refund order A-17", text, StringComparison.Ordinal);
        Assert.Contains("too old to refund", text, StringComparison.Ordinal);
        Assert.Equal(0f, options?.Temperature);
        Assert.IsType<ChatResponseFormatJson>(options?.ResponseFormat);
        Assert.NotNull(((ChatResponseFormatJson?)options?.ResponseFormat)?.Schema);
    }

    [Fact]
    public async Task Only_the_part_of_the_output_the_where_selects_is_shown_and_becomes_the_evidence()
    {
        var model = RecordedModel.Answering("passed.json");
        var trace = Turns([Say("first answer")], [Say("second answer")]);
        var check = PoliteRefusal with { Where = new CheckWhere { Turn = new TurnSelector(TurnSelectorKind.Last) } };

        var verdict = await model.Judge().JudgeAsync(check, Conversation, trace, Cancellation);

        var text = string.Join('\n', Assert.Single(model.Requests).Messages.Select(message => message.Text));
        Assert.Contains("second answer", text, StringComparison.Ordinal);
        Assert.DoesNotContain("first answer", text, StringComparison.Ordinal);
        Assert.Equal("second answer", Assert.IsType<AssistantMessage>(Assert.Single(verdict.Evidence)).Text);
    }
}
