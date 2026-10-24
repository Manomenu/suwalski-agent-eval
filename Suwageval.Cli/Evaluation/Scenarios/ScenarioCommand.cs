using System.CommandLine;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Evaluation.Scenarios;

/// <summary>
/// <c>suwageval scenario &lt;key&gt;</c>: one scenario of the agent named in <c>suwageval/agent.yaml</c> as the server stores it — the
/// conversation, every check with its definition, the revisions and the tags; <c>--json</c> prints the same as one JSON object
/// (<c>scenario</c>, <c>checks</c>, <c>revisions</c>). Exits 0, or 2 when the agent file or the server is wrong or the scenario is unknown.
/// </summary>
internal static class ScenarioCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var key = new Argument<string>("key") { Description = "The scenario's key." };
        var json = CommandOptions.Json("Print the scenario, its checks and revisions as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command("scenario", "Show one scenario: conversation, checks, revisions, tags.")
        {
            key,
            json,
            server,
            dir,
            FromConversationCommand.Create(http),
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var scenarioKey = parsed.GetRequiredValue(key);
                        var scenario = await client.ScenarioAsync(agent, scenarioKey, cancellationToken);
                        var checks = await client.ChecksAsync(agent, scenarioKey, cancellationToken);
                        var revisions = await client.ScenarioRevisionsAsync(agent, scenarioKey, cancellationToken);
                        var shown = new ScenarioView(scenario, checks, revisions);
                        await parsed.InvocationConfiguration.Output.WriteLineAsync(
                            parsed.GetValue(json) ? OutputJson.Of(shown) : ScenarioText.Describe(shown)
                        );
                        return 0;
                    }
                )
        );
        return command;
    }
}
