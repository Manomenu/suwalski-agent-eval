using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Unstable;

/// <summary>
/// <c>suwageval unstable</c>: the checks of the agent named in <c>suwageval/agent.yaml</c> that passed in some finished runs of one version
/// and failed in others — a failure there is noise as much as a regression. One line per check: scenario/check, runs passed of runs, the
/// version; <c>--json</c> prints the records. "No unstable checks." when there are none. Exits 0, or 2 when the agent file or the server is wrong.
/// </summary>
internal static class UnstableCommand
{
    private const string DefaultDir = "suwageval";

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var json = new Option<bool>("--json") { Description = "Print the unstable checks as JSON." };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };
        var dir = new Option<string>("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => DefaultDir };

        var command = new Command("unstable", "List the checks that pass in some runs of a version and fail in others.")
        {
            json,
            server,
            dir,
        };
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
                    var unstable = await new RunClient(transport).UnstableChecksAsync(agent, cancellationToken);
                    var output = parsed.InvocationConfiguration.Output;
                    if (parsed.GetValue(json))
                    {
                        await output.WriteLineAsync(OutputJson.Of(unstable));
                        return 0;
                    }
                    if (unstable.Count == 0)
                    {
                        await output.WriteLineAsync("No unstable checks.");
                        return 0;
                    }
                    foreach (var check in unstable)
                    {
                        await output.WriteLineAsync(Line(check));
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

    private static string Line(UnstableCheck check) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{check.ScenarioKey}/{check.CheckKey}  {check.PassedRuns}/{check.Runs}  version {check.AgentVersionId}{(string.IsNullOrWhiteSpace(check.VersionLabel) ? "" : $" ({check.VersionLabel})")}"
        );
}
