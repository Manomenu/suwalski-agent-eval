using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.AgentErrors;

/// <summary>
/// <c>suwageval agent-errors</c>: the scenarios of the agent named in <c>suwageval/agent.yaml</c> where the agent gave no answer (an HTTP
/// error, a timeout, an unreadable answer) in some samples of the newest finished runs, as long as the newest version that ran the scenario (among <c>--version</c>, else every
/// version not archived) is the one with errors. One line per scenario and version: scenario,
/// errors/samples, the version and the newest error; <c>--json</c> prints the records. "No agent errors." when there are none. Exits 0, or 2
/// when the agent file or the server is wrong.
/// </summary>
internal static class AgentErrorsCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var json = CommandOptions.Json("Print the agent errors as JSON.");
        var versions = CommandOptions.Versions();
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "agent-errors",
            "List the open agent errors: scenarios where the newest version that ran them got no answer (an HTTP error, a timeout)."
        )
        {
            json,
            versions,
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
                        var errors = await client.AgentErrorsAsync(agent, parsed.GetValue(versions) ?? [], cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(errors));
                            return 0;
                        }
                        if (errors.Count == 0)
                        {
                            await output.WriteLineAsync("No agent errors.");
                            return 0;
                        }
                        foreach (var error in errors)
                        {
                            await output.WriteLineAsync(Line(error));
                        }
                        return 0;
                    }
                )
        );
        return command;
    }

    private static string Line(AgentError error) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{error.ScenarioKey}  {error.Errors}/{error.Samples}  version {error.AgentVersionId}{(string.IsNullOrWhiteSpace(error.VersionLabel) ? "" : $" ({error.VersionLabel})")}  {OneLine(error.NewestError)}"
        );

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
