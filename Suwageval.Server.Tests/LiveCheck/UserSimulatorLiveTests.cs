using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.LiveCheck;

namespace Suwageval.Server.Tests.LiveCheck;

/// <summary>
/// The simulated user against the REAL model — left out of the gate, run by <c>scripts/.internal/live-check.sh</c> (AGENTS.md, section 3,
/// "An outside service"). The model comes from the server's own configuration (<c>Models:simulator</c>, else <c>Models:fallback</c>) with the
/// environment on top (<c>Models__fallback__BaseUrl</c>, <c>Models__fallback__Model</c>, from <c>.env</c>).
/// </summary>
public sealed class UserSimulatorLiveTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static ServiceProvider Services()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddEnvironmentVariables()
            .Build();
        return new ServiceCollection().AddSimulation(configuration).BuildServiceProvider();
    }

    [Fact]
    [Trait("live", "true")]
    public async Task The_real_model_plays_the_fixed_case_the_daily_check_uses()
    {
        using var services = Services();
        var simulator = services.GetRequiredService<UserSimulator>();
        Assert.SkipUnless(
            simulator.IsConfigured,
            "No simulator model configured: set Models__fallback__BaseUrl and Models__fallback__Model (.env)."
        );

        Assert.Null(await SimulatorLiveCheck.ProblemAsync(simulator, Cancellation));
    }

    [Fact]
    [Trait("live", "true")]
    public async Task The_real_model_ends_a_conversation_whose_goal_was_reached_and_said_goodbye()
    {
        using var services = Services();
        var simulator = services.GetRequiredService<UserSimulator>();
        Assert.SkipUnless(
            simulator.IsConfigured,
            "No simulator model configured: set Models__fallback__BaseUrl and Models__fallback__Model (.env)."
        );

        var turn = await simulator.NextAsync(
            "You want to know when the shop opens on Saturday. Once you know, you thank the agent and leave. You write in English.",
            [
                new UserMessage { Text = "When do you open on Saturday?" },
                new AssistantMessage { Text = "We open at 9:00 on Saturdays." },
                new UserMessage { Text = "Great, thank you! Bye." },
                new AssistantMessage { Text = "You're welcome, goodbye!" },
            ],
            Cancellation
        );

        Assert.Null(turn.Failure);
        Assert.True(turn.Ended, $"The simulated user wrote instead: {turn.Message}");
    }
}
