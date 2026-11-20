using Microsoft.Extensions.AI;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.LiveCheck;
using Suwageval.Server.Tests.Judges;

namespace Suwageval.Server.Tests.LiveCheck;

public sealed class FallbackModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static RecordedModel StandIn()
    {
        RecordedModel? model = null;
        model = new RecordedModel(_ => Task.FromResult(AnswerTo(model?.Requests[^1].Options)));
        return model;
    }

    private static string AnswerTo(ChatOptions? options)
    {
        var schema = options?.ResponseFormat is ChatResponseFormatJson { Schema: { } json } ? json.ToString() : "";
        return schema switch
        {
            _ when schema.Contains("\"results\"", StringComparison.Ordinal) =>
                """{"results":[{"path":"args.text","correct":true,"reason":"A polite greeting."}]}""",
            _ when schema.Contains("\"passed\"", StringComparison.Ordinal) => """{"passed":true,"reason":"The greeting is polite."}""",
            _ when schema.Contains("\"description\"", StringComparison.Ordinal) =>
                """{"description":"You want to know when the shop opens on Saturday. You write short messages in English."}""",
            _ => """{"message":"When do you open on Saturday?","end":false}""",
        };
    }

    [Fact]
    [Trait("criterion", "AC-simpler-models-and-checks-6")]
    public async Task With_only_the_fallback_model_the_judge_the_simulated_user_and_the_describer_pass_the_daily_check()
    {
        using var model = StandIn();
        var simulator = model.Simulator();
        var describer = model.Describer();

        var judged = await JudgeLiveCheck.ProblemAsync(model.Judge(), model.ArgumentJudge(), Cancellation);
        var played = await SimulatorLiveCheck.ProblemAsync(simulator, Cancellation);
        var described = await describer.DescribeAsync(
            [new UserMessage { Text = "When does the shop open on Saturday?" }, new AssistantMessage { Text = "At 10:00." }],
            Cancellation
        );

        Assert.Null(judged);
        Assert.Null(played);
        Assert.Null(described.Notice);
        Assert.Equal(("fallback", "fallback"), (simulator.ModelEntry, describer.ModelEntry));
        Assert.Equal(4, model.Requests.Count);
    }
}
