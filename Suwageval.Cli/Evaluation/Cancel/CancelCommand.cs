using System.CommandLine;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Cancel;

internal static class CancelCommand
{
    public static Command Create(HttpClient? http = null)
    {
        var runId = new Argument<Guid>("runId") { Description = "The run to stop, as `suwageval run` printed it." };
        var server = CommandOptions.Server();

        var command = new Command("cancel", "Stop a queued or running run. A run that is already over stays as it is.") { runId, server };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, site) =>
                    {
                        var output = parsed.InvocationConfiguration.Output;
                        var id = parsed.GetRequiredValue(runId);
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
                )
        );
        return command;
    }
}
