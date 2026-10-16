using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Runs;

/// <summary>
/// <c>suwageval runs</c>: the runs of the agent named in <c>suwageval/agent.yaml</c>, newest first — id, status, tag, version, checks
/// passed of all, creation time; <c>--limit</c> keeps the newest ones (default 10), <c>--json</c> prints the run summaries. It is how a
/// run to compare with is found (<c>suwageval compare</c>). Exits 0, or 2 when the agent file or the server is wrong.
/// </summary>
internal static class RunsCommand
{
    private const string DefaultDir = "suwageval";
    private const int DefaultLimit = 10;

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var limit = new Option<int>("--limit")
        {
            Description = "How many of the newest runs to list.",
            DefaultValueFactory = _ => DefaultLimit,
        };
        var json = new Option<bool>("--json") { Description = "Print the runs as JSON." };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };
        var dir = new Option<string>("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => DefaultDir };

        var command = new Command("runs", "List the agent's newest runs.") { limit, json, server, dir };
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var error = parsed.InvocationConfiguration.Error;
                var agent = AgentName.Read(parsed.GetRequiredValue(dir), error);
                if (agent is null)
                {
                    return 2;
                }
                HttpClient? owned = null;
                try
                {
                    var transport =
                        http ?? (owned = new HttpClient { BaseAddress = ServerAddress.Api(ServerAddress.Site(parsed.GetValue(server))) });
                    var runs = (await new RunClient(transport).RunsAsync(agent, cancellationToken)).Take(parsed.GetValue(limit)).ToList();
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
                catch (RunClientException failure)
                {
                    await error.WriteLineAsync(failure.Message);
                    return 2;
                }
                finally
                {
                    owned?.Dispose();
                }
            }
        );
        return command;
    }
}
