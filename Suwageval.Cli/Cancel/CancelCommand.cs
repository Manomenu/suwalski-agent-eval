using System.CommandLine;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Cancel;

/// <summary>
/// <c>suwageval cancel &lt;runId&gt;</c>: asks the server to stop a run. A queued run is cancelled at once, a running one finishes the
/// samples in flight and stops before the next. A run that is already over is left as it is and said so. Prints the run's page; exits 0,
/// or 2 for an unknown run or a server that does not answer.
/// </summary>
internal static class CancelCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var runId = new Argument<Guid>("runId") { Description = "The run to stop, as `suwageval run` printed it." };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };

        var command = new Command("cancel", "Stop a queued or running run. A run that is already over stays as it is.") { runId, server };
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var output = parsed.InvocationConfiguration.Output;
                var site = ServerAddress.Site(parsed.GetValue(server));
                var transport = http ?? new HttpClient { BaseAddress = ServerAddress.Api(site) };
                var id = parsed.GetRequiredValue(runId);
                try
                {
                    var client = new RunClient(transport);
                    var run = await client.GetAsync(id, cancellationToken);
                    if (run.Status is RunStatus.Queued or RunStatus.Running)
                    {
                        await client.CancelAsync(id, cancellationToken);
                        await output.WriteLineAsync($"Cancelling run {id}.");
                    }
                    else
                    {
                        await output.WriteLineAsync($"Run {id} is already {run.Status}.");
                    }
                    await output.WriteLineAsync(ServerAddress.RunPage(site, id).ToString());
                    return 0;
                }
                catch (RunClientException failure)
                {
                    await parsed.InvocationConfiguration.Error.WriteLineAsync(failure.Message);
                    return 2;
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
