using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Server.Judges;
using static Suwageval.Server.Tests.Checks.Items;

namespace Suwageval.Server.Tests.Judges;

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
    public async Task The_real_judge_model_judges_the_fixed_case_the_daily_check_uses()
    {
        using var services = Services();
        var judge = services.GetRequiredService<Judge>();
        Assert.SkipUnless(judge.IsConfigured, "No judge model configured: set Models__judge__BaseUrl and Models__judge__Model (.env).");

        var problem = await JudgeLiveCheck.ProblemAsync(judge, Cancellation);

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
}
