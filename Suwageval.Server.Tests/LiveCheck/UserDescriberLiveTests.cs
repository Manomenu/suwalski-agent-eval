using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.Evaluation.Simulation;

namespace Suwageval.Server.Tests.LiveCheck;

/// <summary>
/// The user describer against the REAL model — left out of the gate, run by <c>scripts/.internal/live-check.sh</c> (AGENTS.md, section 3,
/// "An outside service"). The model comes from the server's own configuration (<c>Models:describer</c>, else <c>Models:fallback</c>) with the
/// environment on top (<c>Models__fallback__BaseUrl</c>, <c>Models__fallback__Model</c>, from <c>.env</c>).
/// </summary>
public sealed class UserDescriberLiveTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("live", "true")]
    public async Task The_real_model_describes_the_user_of_a_short_conversation_in_the_second_person()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddEnvironmentVariables()
            .Build();
        using var services = new ServiceCollection().AddSimulation(configuration).BuildServiceProvider();
        var describer = services.GetRequiredService<UserDescriber>();
        Assert.SkipUnless(
            describer.ModelEntry is not null,
            "No describer model configured: set Models__fallback__BaseUrl and Models__fallback__Model (.env)."
        );

        var description = await describer.DescribeAsync(
            [
                new UserMessage { Text = "My kettle from order A-17 broke after a month." },
                new AssistantMessage { Text = "Sorry to hear that. Would you like a repair?" },
                new UserMessage { Text = "No, I want my money back." },
            ],
            Cancellation
        );

        Assert.Null(description.Notice);
        Assert.StartsWith("You", description.Text, StringComparison.Ordinal);
        Assert.Contains("A-17", description.Text, StringComparison.Ordinal);
    }
}
