using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Run;

/// <summary>
/// <c>suwageval run</c>: starts a run of the agent named in <c>suwageval/agent.yaml</c> on the server, which reads the
/// agent's contract and runs against that version (the output says which, and whether it is new). With <c>--wait</c> it prints
/// one line of totals and the link to the run's page (the results are read there, or with <c>--json</c>), and exits 0 unless a check regressed or is new and failing
/// (<c>--strict</c>: 1 when any check missed its threshold), 2 when the run could not be started, failed or was cancelled. Ctrl+C while
/// waiting asks whether to cancel the run on the server too (<see cref="RunOutput"/>); left running, the exit code is 130.
/// </summary>
internal static class RunCommand
{
    /// <param name="terminal">Where Ctrl+C during <c>--wait</c> asks whether to cancel the run too.</param>
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    /// <param name="origins">Reads where the agent's repository stands; none means the real one and the CI environment (the tests pass their own).</param>
    public static Command Create(Terminal terminal, HttpClient? http = null, RunOriginReader? origins = null)
    {
        var tag = new Option<string?>("--tag") { Description = "Only the scenarios with this tag; default every scenario." };
        var endpoint = new Option<string?>("--endpoint")
        {
            Description = AgentEndpoint.OptionDescription(
                "the endpointUrl the agent was registered with (its contract is read from there too)."
            ),
        };
        var reference = new Option<string?>("--ref")
        {
            Description = "Compare with the reference runs of this branch, instead of the one the run picks from the repository.",
        };
        var wait = CommandOptions.Wait("run");
        var json = CommandOptions.Json("Print JSON: the results with --wait, otherwise { runId }.");
        var strict = CommandOptions.Strict();
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "run",
            "Run the agent's scenarios on the server. With --wait: exit 0 unless a check regressed or is new and failing (--strict: unless any check fails), 2 when the run cannot finish."
        )
        {
            tag,
            endpoint,
            reference,
            wait,
            json,
            strict,
            server,
            dir,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
            {
                var output = parsed.InvocationConfiguration.Output;
                var error = parsed.InvocationConfiguration.Error;
                var agentDir = parsed.GetRequiredValue(dir);
                var agent = AgentReader.Read(agentDir, error)?.Definition.Name;
                if (agent is null)
                {
                    return Task.FromResult(2);
                }
                return ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    error,
                    (client, site) =>
                        RunOutput.GuardAsync(
                            async () =>
                            {
                                var created = await client.CreateAsync(
                                    new RunRequest
                                    {
                                        Agent = agent,
                                        Tag = parsed.GetValue(tag),
                                        EndpointUrl = AgentEndpoint.Override(parsed.GetValue(endpoint)),
                                        Origin = (origins ?? RunOriginReader.System).Read(agentDir, parsed.GetValue(reference)),
                                    },
                                    cancellationToken
                                );
                                return await RunOutput.ReportAsync(
                                    client,
                                    created,
                                    parsed.GetValue(wait),
                                    parsed.GetValue(json),
                                    parsed.GetValue(strict),
                                    site,
                                    output,
                                    error,
                                    terminal,
                                    cancellationToken
                                );
                            },
                            error,
                            "run",
                            cancellationToken
                        ),
                    agentDir: agentDir
                );
            }
        );
        return command;
    }
}
