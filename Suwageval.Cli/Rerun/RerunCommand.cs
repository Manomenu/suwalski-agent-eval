using System.CommandLine;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Rerun;

/// <summary>
/// <c>suwageval rerun</c>: a new run with the agent, tags and endpoint of an earlier one (<c>--endpoint</c> replaces the endpoint).
/// The version is the one the agent serves now. Output and exit codes are those of <c>suwageval run</c>.
/// </summary>
internal static class RerunCommand
{
    /// <param name="terminal">Where Ctrl+C during <c>--wait</c> asks whether to cancel the run too.</param>
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(Terminal terminal, HttpClient? http = null)
    {
        var runId = new Argument<Guid>("runId") { Description = "The run to repeat." };
        var endpoint = new Option<string?>("--endpoint")
        {
            Description = AgentEndpoint.OptionDescription("the endpoint of that run (its contract is read from there too)."),
        };
        var group = CommandOptions.RunGroup();
        var note = CommandOptions.RunNote();
        var wait = CommandOptions.Wait("run");
        var json = CommandOptions.Json("Print JSON: the results with --wait, otherwise { runId }.");
        var strict = CommandOptions.Strict();
        var server = CommandOptions.Server();

        var command = new Command(
            "rerun",
            "Run again with the agent, tags and endpoint of an earlier run, against the version the agent serves now. With --wait: exit 0 unless a check regressed or is new and failing (--strict: unless any check fails), 2 when the run cannot finish."
        )
        {
            runId,
            endpoint,
            group,
            note,
            wait,
            json,
            strict,
            server,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
            {
                var error = parsed.InvocationConfiguration.Error;
                return ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    error,
                    (client, site) =>
                        RunOutput.GuardAsync(
                            async () =>
                            {
                                var created = await client.RerunAsync(
                                    parsed.GetRequiredValue(runId),
                                    AgentEndpoint.Override(parsed.GetValue(endpoint)),
                                    parsed.GetValue(group),
                                    parsed.GetValue(note),
                                    cancellationToken
                                );
                                return await RunOutput.ReportAsync(
                                    client,
                                    created,
                                    parsed.GetValue(wait),
                                    parsed.GetValue(json),
                                    parsed.GetValue(strict),
                                    site,
                                    parsed.InvocationConfiguration.Output,
                                    error,
                                    terminal,
                                    cancellationToken
                                );
                            },
                            error,
                            "run",
                            cancellationToken
                        )
                );
            }
        );
        return command;
    }
}
