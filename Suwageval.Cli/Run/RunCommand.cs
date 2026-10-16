using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Run;

/// <summary>
/// <c>suwageval run</c>: starts a run of the agent named in <c>suwageval/agent.yaml</c> on the server, which reads the
/// agent's contract and runs against that version (the output says which, and whether it is new). With <c>--wait</c> it prints
/// one line of totals and the link to the run's page (the results are read there, or with <c>--json</c>), and exits 0 when every
/// check reached its threshold, 1 when one did not, 2 when the run could not be started, failed or was cancelled. Ctrl+C while
/// waiting asks whether to cancel the run on the server too (<see cref="RunOutput"/>); left running, the exit code is 130.
/// </summary>
internal static class RunCommand
{
    private const string DefaultDir = "suwageval";

    /// <param name="terminal">Where Ctrl+C during <c>--wait</c> asks whether to cancel the run too.</param>
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(Terminal terminal, HttpClient? http = null)
    {
        var tag = new Option<string?>("--tag") { Description = "Only the scenarios with this tag; default every scenario." };
        var endpoint = new Option<string?>("--endpoint")
        {
            Description = "Reach the agent here (its contract is read from here too) instead of at the endpointUrl it was registered with.",
        };
        var wait = new Option<bool>("--wait")
        {
            Description =
                "Wait for the run to finish and print its totals and the link to its page (see the exit codes). Ctrl+C asks whether to cancel the run too.",
        };
        var json = new Option<bool>("--json") { Description = "Print JSON: the results with --wait, otherwise { runId }." };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };
        var dir = new Option<string>("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => DefaultDir };

        var command = new Command(
            "run",
            "Run the agent's scenarios on the server. With --wait: exit 0 when all checks pass, 1 when a check fails, 2 when the run cannot finish."
        )
        {
            tag,
            endpoint,
            wait,
            json,
            server,
            dir,
        };
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var output = parsed.InvocationConfiguration.Output;
                var error = parsed.InvocationConfiguration.Error;
                var agent = AgentName.Read(parsed.GetRequiredValue(dir), error);
                if (agent is null)
                {
                    return 2;
                }
                var site = ServerAddress.Site(parsed.GetValue(server));
                var transport = http ?? new HttpClient { BaseAddress = ServerAddress.Api(site) };
                try
                {
                    var client = new RunClient(transport);
                    var created = await client.CreateAsync(
                        new RunRequest
                        {
                            Agent = agent,
                            Tag = parsed.GetValue(tag),
                            EndpointUrl = parsed.GetValue(endpoint),
                        },
                        cancellationToken
                    );
                    return await RunOutput.ReportAsync(
                        client,
                        created,
                        parsed.GetValue(wait),
                        parsed.GetValue(json),
                        site,
                        output,
                        error,
                        terminal,
                        cancellationToken
                    );
                }
                catch (RunClientException failure)
                {
                    await error.WriteLineAsync(failure.Message);
                    return 2;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Ctrl+C before the server answered: the run may exist already, and is then left as it is.
                    await parsed.InvocationConfiguration.Error.WriteLineAsync(
                        "Interrupted; if the run was started, `suwageval runs` lists it."
                    );
                    return RunOutput.Interrupted;
                }
                finally
                {
                    if (http is null)
                    {
                        transport.Dispose();
                    }
                }
            }
        );
        return command;
    }
}
