using System.CommandLine;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Rerun;

/// <summary>
/// <c>suwageval rerun</c>: a new run with the agent, tag and endpoint of an earlier one (<c>--endpoint</c> replaces the endpoint).
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
            Description = "Reach the agent here (its contract is read from here too) instead of at the endpoint of that run.",
        };
        var wait = new Option<bool>("--wait")
        {
            Description =
                "Wait for the run to finish and print its totals and the link to its page (see the exit codes). Ctrl+C asks whether to cancel the run too.",
        };
        var json = new Option<bool>("--json") { Description = "Print JSON: the results with --wait, otherwise { runId }." };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };

        var command = new Command(
            "rerun",
            "Run again with the agent, tag and endpoint of an earlier run, against the version the agent serves now. With --wait: exit 0 when all checks pass, 1 when a check fails, 2 when the run cannot finish."
        )
        {
            runId,
            endpoint,
            wait,
            json,
            server,
        };
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var site = ServerAddress.Site(parsed.GetValue(server));
                var transport = http ?? new HttpClient { BaseAddress = ServerAddress.Api(site) };
                try
                {
                    var client = new RunClient(transport);
                    var created = await client.RerunAsync(parsed.GetRequiredValue(runId), parsed.GetValue(endpoint), cancellationToken);
                    return await RunOutput.ReportAsync(
                        client,
                        created,
                        parsed.GetValue(wait),
                        parsed.GetValue(json),
                        site,
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        terminal,
                        cancellationToken
                    );
                }
                catch (RunClientException failure)
                {
                    await parsed.InvocationConfiguration.Error.WriteLineAsync(failure.Message);
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
