using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Judges;
using Suwageval.Server.LiveCheck;
using static Suwageval.Server.Tests.Evaluation.Checks.Items;

namespace Suwageval.Server.Tests.LiveCheck;

/// <summary>
/// The judge against the REAL model — left out of the gate (<c>check.sh</c> filters the trait), run by
/// <c>scripts/.internal/live-check.sh</c>: the model can change its answer without a commit here (AGENTS.md, section 3,
/// "An outside service"). The model comes from the server's own configuration (<c>appsettings.json</c>, copied to the test output:
/// <c>Judge:Model</c>) with the environment on top (<c>Models__judge__BaseUrl</c>, <c>Models__judge__Model</c>, from <c>.env</c>).
/// </summary>
public sealed class JudgeLiveTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static ServiceProvider Services()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddEnvironmentVariables()
            .Build();
        return new ServiceCollection().AddJudges(configuration).BuildServiceProvider();
    }

    [Fact]
    [Trait("live", "true")]
    public async Task The_real_judge_model_judges_the_fixed_cases_the_daily_check_uses()
    {
        using var services = Services();
        var judge = services.GetRequiredService<Judge>();
        Assert.SkipUnless(judge.IsConfigured, "No judge model configured: set Models__judge__BaseUrl and Models__judge__Model (.env).");

        var problem = await JudgeLiveCheck.ProblemAsync(judge, services.GetRequiredService<ArgumentJudge>(), Cancellation);

        Assert.Null(problem);
    }

    [Fact]
    [Trait("live", "true")]
    public async Task The_real_judge_model_fails_an_output_that_plainly_breaks_the_criteria()
    {
        using var services = Services();
        var judge = services.GetRequiredService<Judge>();
        Assert.SkipUnless(judge.IsConfigured, "No judge model configured: set Models__judge__BaseUrl and Models__judge__Model (.env).");
        var check = new JudgeCheck
        {
            Key = "polite",
            ScenarioKey = "s",
            Criteria = "The assistant is polite to the user.",
        };

        var verdict = await judge.JudgeAsync(
            check,
            [new UserMessage { Text = "Where is my order?" }],
            Turn(Say("Stop bothering me, you idiot.")),
            Cancellation
        );

        Assert.False(verdict.NotJudged, verdict.Message);
        Assert.False(verdict.Passed, verdict.Message);
    }

    private static readonly ToolCalledCheck ReasonCheck = new()
    {
        Key = "reason",
        ScenarioKey = "s",
        Tool = "refund_order",
        Args = Json(
            /*lang=json,strict*/
            """
            {"order_id":"A-17","reason":{"$judge":{"description":"Says the item arrived damaged or broken","examples":["kettle arrived broken","damaged on delivery"]}}}
            """
        ),
    };

    [Theory]
    [Trait("live", "true")]
    [InlineData("The kettle was cracked when I opened the box.", true)]
    [InlineData("I changed my mind and want my money back.", false)]
    public async Task The_real_judge_model_judges_an_argument_against_a_description_and_examples(string reason, bool fits)
    {
        using var services = Services();
        Assert.SkipUnless(
            services.GetRequiredService<Judge>().IsConfigured,
            "No judge model configured: set Models__judge__BaseUrl and Models__judge__Model (.env)."
        );
        var arguments = JsonSerializer.Serialize(new { order_id = "A-17", reason });

        var verdict = await services
            .GetRequiredService<ArgumentJudge>()
            .JudgeAsync(ReasonCheck, Turn(Call("refund_order", arguments)), Cancellation);

        Assert.False(verdict.NotJudged, verdict.Message);
        Assert.Equal(fits, verdict.Passed);
    }
}
