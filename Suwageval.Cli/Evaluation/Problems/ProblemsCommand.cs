using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Problems;

/// <summary>
/// <c>suwageval problems</c>: what is wrong with the agent named in <c>suwageval/agent.yaml</c> beyond one run — the web's "Problems". Its
/// unstable checks (passed in some finished runs of one version and failed in others, so a failure there may be noise) and its agent
/// errors (scenarios where the agent gave no answer: an HTTP error, a timeout, an unreadable answer), each open as long as the newest
/// version that ran it (among <c>--version</c>, else every version not archived) is the one with the problem. Runs started in a group are
/// not counted. One line per problem under its heading; <c>--json</c> prints <c>{ unstableChecks, agentErrors }</c>. Exits 0, or 2 when the
/// agent file or the server is wrong.
/// </summary>
internal static class ProblemsCommand
{
    /// <summary>What <c>--json</c> prints.</summary>
    internal sealed record Found(IReadOnlyList<UnstableCheck> UnstableChecks, IReadOnlyList<AgentError> AgentErrors);

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var json = CommandOptions.Json("Print the unstable checks and the agent errors as JSON.");
        var versions = CommandOptions.Versions();
        var dataVersions = CommandOptions.DataVersions();
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "problems",
            "List the agent's open problems beyond one run: unstable checks (pass in some runs of a version, fail in others) and agent errors (scenarios the agent gave no answer in)."
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
                        var picked = parsed.GetValue(versions) ?? [];
                        var data = parsed.GetValue(dataVersions) ?? [];
                        var unstable = await client.UnstableChecksAsync(agent, picked, data, cancellationToken);
                        var errors = await client.AgentErrorsAsync(agent, picked, data, cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(new Found(unstable, errors)));
                            return 0;
                        }
                        if (unstable.Count == 0 && errors.Count == 0)
                        {
                            await output.WriteLineAsync("No unstable checks and no agent errors.");
                            return 0;
                        }
                        if (unstable.Count > 0)
                        {
                            await output.WriteLineAsync("Unstable checks (a failure there may be noise):");
                            foreach (var check in unstable)
                            {
                                await output.WriteLineAsync($"  {Line(check)}");
                            }
                        }
                        if (errors.Count > 0)
                        {
                            await output.WriteLineAsync("Agent errors (no answer, so the checks were not evaluated):");
                            foreach (var error in errors)
                            {
                                await output.WriteLineAsync($"  {Line(error)}");
                            }
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
            $"{check.ScenarioKey}/{check.CheckKey}  {check.PassedRuns}/{check.Runs}  version {check.AgentVersionId}{Label(check.VersionLabel)}"
        );

    private static string Line(AgentError error) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{error.ScenarioKey}  {error.Errors}/{error.Samples}  version {error.AgentVersionId}{Label(error.VersionLabel)}  {OneLine(error.NewestError)}"
        );

    private static string Label(string? label) => string.IsNullOrWhiteSpace(label) ? "" : $" ({label})";

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
