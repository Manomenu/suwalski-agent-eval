using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Judges;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.LiveCheck;

// Why: the model is not ours and can change its answer without a commit here, so this daily check is what notices
internal static class JudgeLiveCheck
{
    private const string Criteria = "The assistant greets the user politely.";

    private static readonly JudgeCheck Check = new()
    {
        Key = "live-check",
        ScenarioKey = "live-check",
        Criteria = Criteria,
    };

    private static readonly ToolCalledCheck ArgumentCheck = new()
    {
        Key = "live-check-argument",
        ScenarioKey = "live-check",
        Tool = "send_message",
        Args = JsonSerializer.Deserialize<JsonElement>(
            """{"text":{"$judge":{"description":"A polite greeting","examples":["Hello! How can I help you today?"]}}}"""
        ),
    };

    private static readonly Trace ArgumentOutput = new([
        [
            new ToolCall
            {
                Id = "live-check-call",
                Name = "send_message",
                Arguments = JsonSerializer.Deserialize<JsonElement>("""{"text":"Hello! It is nice to hear from you."}"""),
            },
        ],
    ]);

    private static readonly UserMessage[] Conversation = [new UserMessage { Text = "Hello!" }];

    private static readonly Trace Output = new([
        [new AssistantMessage { Text = "Hello! It is nice to hear from you. How can I help?" }],
    ]);

    public static async Task<string?> ProblemAsync(Judge judge, ArgumentJudge argumentJudge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(judge);
        ArgumentNullException.ThrowIfNull(argumentJudge);
        var verdict = await judge.JudgeAsync(Check, Conversation, Output, cancellationToken);
        if (Problem(verdict, "The judge failed a plainly polite greeting") is { } problem)
        {
            return problem;
        }
        var argument = await argumentJudge.JudgeAsync(ArgumentCheck, ArgumentOutput, cancellationToken);
        return Problem(argument, "The $judge argument matcher failed a plainly polite greeting");
    }

    private static string? Problem(CheckVerdict verdict, string failed) =>
        (verdict.NotJudged, verdict.Passed) switch
        {
            (true, _) => verdict.Message,
            (false, false) => $"{failed}: {verdict.Message}",
            _ => null,
        };

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever goes wrong is the check's result.")]
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "A one-shot command of the CronJob: what it prints is its report, read as it is in the job's log."
    )]
    public static async Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddJudges(builder.Configuration);
        builder.Services.AddSimulation(builder.Configuration);
        builder.Services.AddOptions<LiveCheckOptions>().Bind(builder.Configuration.GetSection(LiveCheckOptions.Section)).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<LiveCheckOptions>, LiveCheckOptions.LiveCheckOptionsValidator>();
        using var host = builder.Build();
        // Why: validates the options (ValidateOnStart) without running a service of ours
        await host.StartAsync();

        var problem =
            await ProblemAsync(
                host.Services.GetRequiredService<Judge>(),
                host.Services.GetRequiredService<ArgumentJudge>(),
                CancellationToken.None
            )
            ?? await SimulatorLiveCheck.ProblemAsync(host.Services.GetRequiredService<UserSimulator>(), CancellationToken.None)
            ?? await DescriberLiveCheck.ProblemAsync(host.Services.GetRequiredService<UserDescriber>(), CancellationToken.None);
        if (problem is null)
        {
            var entry = host.Services.GetRequiredService<UserSimulator>().ModelEntry;
            await Console.Out.WriteLineAsync(
                "The models answered as expected (a judge check, a $judge argument"
                    + (entry is null ? "" : $", the simulated user through the model entry '{entry}'")
                    + (host.Services.GetRequiredService<UserDescriber>().HasOwnModel ? ", the user describer through 'describer'" : "")
                    + ")."
            );
            return 0;
        }

        await Console.Error.WriteLineAsync($"The model live check failed: {problem}");
        if (host.Services.GetRequiredService<IOptions<LiveCheckOptions>>().Value.DiscordWebhookUrl is { } webhook)
        {
            try
            {
                using var http = new HttpClient();
                using var response = await http.PostAsJsonAsync(
                    webhook,
                    new { content = $"suwageval: the model live check failed. {problem}" }
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
