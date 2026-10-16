using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Server.Checks;

namespace Suwageval.Server.Judges;

/// <summary>
/// The daily check from production's network (<c>Suwageval.Server live-check</c>, the chart's CronJob): one judge call with a
/// fixed tiny case, through the production registration of the model and the judge. The model is not ours, so it can change
/// its answer without a commit here; this is what notices. A problem is logged, posted to the Discord alerts channel when a
/// webhook is configured, and makes the process exit non-zero. It writes nothing anywhere, so there is nothing to roll back.
/// </summary>
internal static class JudgeLiveCheck
{
    private const string Criteria = "The assistant greets the user politely.";

    private static readonly JudgeCheck Check = new()
    {
        Key = "live-check",
        ScenarioKey = "live-check",
        Criteria = Criteria,
    };

    private static readonly UserMessage[] Conversation = [new UserMessage { Text = "Hello!" }];

    private static readonly Trace Output = new([
        [new AssistantMessage { Text = "Hello! It is nice to hear from you. How can I help?" }],
    ]);

    /// <summary>What is wrong with the judge right now, in a sentence, or null when it judged the fixed case as expected (passed).</summary>
    public static async Task<string?> ProblemAsync(Judge judge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(judge);
        var verdict = await judge.JudgeAsync(Check, Conversation, Output, cancellationToken);
        return (verdict.NotJudged, verdict.Passed) switch
        {
            (true, _) => verdict.Message,
            (false, false) => $"The judge failed a plainly polite greeting: {verdict.Message}",
            _ => null,
        };
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever goes wrong is the check's result.")]
    public static async Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddJudges(builder.Configuration);
        builder.Services.AddOptions<LiveCheckOptions>().Bind(builder.Configuration.GetSection(LiveCheckOptions.Section)).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<LiveCheckOptions>, LiveCheckOptions.Validator>();
        using var host = builder.Build();
        await host.StartAsync(); // validates the options (ValidateOnStart) and runs no service of ours

        var problem = await ProblemAsync(host.Services.GetRequiredService<Judge>(), CancellationToken.None);
        if (problem is null)
        {
            await Console.Out.WriteLineAsync("The judge model answered as expected.");
            return 0;
        }

        await Console.Error.WriteLineAsync($"The judge model live check failed: {problem}");
        if (host.Services.GetRequiredService<IOptions<LiveCheckOptions>>().Value.DiscordWebhookUrl is { } webhook)
        {
            try
            {
                using var http = new HttpClient();
                using var response = await http.PostAsJsonAsync(
                    webhook,
                    new { content = $"suwageval: the judge model live check failed. {problem}" }
                );
                response.EnsureSuccessStatusCode();
            }
            catch (Exception error)
            {
                await Console.Error.WriteLineAsync($"Posting to Discord failed: {error.Message}");
            }
        }
        return 1;
    }
}
