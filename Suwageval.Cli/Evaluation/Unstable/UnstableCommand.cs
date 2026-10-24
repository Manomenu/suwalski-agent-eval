using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Unstable;

/// <summary>
/// <c>suwageval unstable</c>: the checks of the agent named in <c>suwageval/agent.yaml</c> that passed in some finished runs of one version
/// and failed in others — a failure there is noise as much as a regression — as long as the newest version that ran the check at least
/// twice (among <c>--version</c>, else every version not archived) is the unstable one. One line per check: scenario/check, runs passed of runs, the
/// version; <c>--json</c> prints the records. "No unstable checks." when there are none. Exits 0, or 2 when the agent file or the server is wrong.
/// </summary>
internal static class UnstableCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var json = CommandOptions.Json("Print the unstable checks as JSON.");
        var versions = CommandOptions.Versions();
        var dataVersions = CommandOptions.DataVersions();
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "unstable",
            "List the open unstable checks: unstable in the newest version that ran them at least twice (runs started in a group are not counted)."
        )
        {
            json,
            versions,
            dataVersions,
            server,
            dir,
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
                        var unstable = await client.UnstableChecksAsync(
                            agent,
                            parsed.GetValue(versions) ?? [],
                            parsed.GetValue(dataVersions) ?? [],
                            cancellationToken
                        );
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
                )
        );
        return command;
    }

    private static string Line(UnstableCheck check) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{check.ScenarioKey}/{check.CheckKey}  {check.PassedRuns}/{check.Runs}  version {check.AgentVersionId}{(string.IsNullOrWhiteSpace(check.VersionLabel) ? "" : $" ({check.VersionLabel})")}"
        );
}
