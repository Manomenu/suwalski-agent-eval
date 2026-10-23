using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Scenarios;

/// <summary>
/// <c>suwageval scenarios</c>: the scenarios of the agent named in <c>suwageval/agent.yaml</c> — key, revision, number of checks, tags;
/// <c>--tag</c> keeps those with the tag, <c>--json</c> prints the scenario summaries. Exits 0, or 2 when the agent file or the server is wrong.
/// </summary>
internal static class ScenariosCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var tag = new Option<string?>("--tag") { Description = "Only the scenarios with this tag." };
        var json = CommandOptions.Json("Print the scenarios as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command("scenarios", "List the agent's scenarios.") { tag, json, server, dir };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var scenarios = await client.ScenariosAsync(agent, parsed.GetValue(tag), cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(scenarios));
                            return 0;
                        }
                        var checks = (await client.ChecksAsync(agent, null, cancellationToken)).ToLookup(check => check.Check.ScenarioKey);
                        if (scenarios.Count == 0)
                        {
                            await output.WriteLineAsync("No scenarios.");
                        }
                        foreach (var scenario in scenarios)
                        {
                            var count = checks[scenario.Key].Count();
                            await output.WriteLineAsync(
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"{scenario.Key}  revision {scenario.Revision}  {count} check{(count == 1 ? "" : "s")}  tags: {(scenario.Tags.Count == 0 ? "-" : string.Join(", ", scenario.Tags))}"
                                )
                            );
                        }
                        return 0;
                    }
                )
        );
        return command;
    }
}
