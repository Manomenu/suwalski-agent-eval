using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Agents;

/// <summary>
/// <c>suwageval agents</c>: every agent on the server — name, endpoint, creation time; <c>--json</c> prints the records. It needs no
/// <c>agent.yaml</c>: it is how an agent is found when the repository does not say which one to look at. Exits 0, or 2 when the server is wrong.
/// </summary>
internal static class AgentsCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var json = CommandOptions.Json("Print the agents as JSON.");
        var server = CommandOptions.Server();

        var command = new Command("agents", "List the agents on the server.") { json, server };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, _) =>
                    {
                        var agents = await client.AgentsAsync(cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(agents));
                            return 0;
                        }
                        if (agents.Count == 0)
                        {
                            await output.WriteLineAsync("No agents.");
                        }
                        foreach (var agent in agents)
                        {
                            await output.WriteLineAsync(
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"{agent.Name}  {agent.EndpointUrl}  created {agent.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z"
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
