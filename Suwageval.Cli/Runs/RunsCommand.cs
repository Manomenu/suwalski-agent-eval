using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Runs;

/// <summary>
/// <c>suwageval runs</c>: the runs of the agent named in <c>suwageval/agent.yaml</c>, newest first — id, status, tag, version, checks
/// passed of all, creation time; <c>--limit</c> keeps the newest ones (default 10), <c>--json</c> prints the run summaries. It is how a
/// run to compare with is found (<c>suwageval compare</c>). Exits 0, or 2 when the agent file or the server is wrong.
/// </summary>
internal static class RunsCommand
{
    private const int DefaultLimit = 10;

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var limit = new Option<int>("--limit")
        {
            Description = "How many of the newest runs to list.",
            DefaultValueFactory = _ => DefaultLimit,
        };
        var json = CommandOptions.Json("Print the runs as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command("runs", "List the agent's newest runs.") { limit, json, server, dir };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var runs = await client.RunsAsync(agent, parsed.GetValue(limit), cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(runs));
                            return 0;
                        }
                        foreach (var run in runs)
                        {
                            await output.WriteLineAsync(
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"{run.Id}  {run.Status}  {run.Tag ?? "-"}  version {run.AgentVersionId}  {run.ChecksPassed}/{run.ChecksPassed + run.ChecksFailed} checks passed  {run.CreatedAt:yyyy-MM-dd HH:mm}Z"
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
