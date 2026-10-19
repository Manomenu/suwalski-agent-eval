using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Agents;

/// <summary>
/// <c>suwageval agent</c>: what the server stores for the agent named in <c>suwageval/agent.yaml</c> — endpoint, creation time, how many
/// versions, scenarios and checks, the default samples and the regression settings; <c>--json</c> prints the record. Exits 0, or 2 when
/// the agent file or the server is wrong.
/// </summary>
internal static class AgentCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var json = CommandOptions.Json("Print the agent as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command("agent", "Show what the server stores for this repository's agent.") { json, server, dir };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var detail = await client.AgentAsync(agent, cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(detail));
                            return 0;
                        }
                        await output.WriteLineAsync(
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{detail.Name}  {detail.EndpointUrl}  created {detail.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z"
                            )
                        );
                        await output.WriteLineAsync(
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{detail.Versions} versions  {detail.Scenarios} scenarios  {detail.Checks} checks  samples {detail.Samples?.ToString(CultureInfo.InvariantCulture) ?? "default"}"
                            )
                        );
                        if (detail.Regression is { } regression)
                        {
                            await output.WriteLineAsync(
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"regression: branches {(regression.Branches is { Count: > 0 } ? string.Join(", ", regression.Branches) : "default")}  history {regression.History?.ToString(CultureInfo.InvariantCulture) ?? "default"}"
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
