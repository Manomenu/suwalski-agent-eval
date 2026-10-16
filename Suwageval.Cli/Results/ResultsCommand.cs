using System.CommandLine;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Results;

/// <summary>
/// <c>suwageval results &lt;runId&gt;</c>: opens the run's page on the web, where its results are read — the link is printed too, for
/// a terminal with no browser. With <c>--json</c> it opens nothing: it waits for the run to end, prints its results as JSON and exits
/// like <c>run --wait</c> (0 every check passed, 1 one did not, 2 the run did not finish). Either way the run is asked for first
/// through the web's address, so a run that does not exist or a web that does not answer exits 2.
/// </summary>
internal static class ResultsCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    /// <param name="openBrowser">Opens a page; none means the system's default browser (the tests record the page instead).</param>
    public static Command Create(HttpClient? http, Action<Uri>? openBrowser)
    {
        var runId = new Argument<Guid>("runId") { Description = "The run, as `suwageval run` printed it." };
        var json = new Option<bool>("--json")
        {
            Description = "Print the results as JSON instead of opening the page; waits for the run to end and exits by its checks.",
        };
        var server = CommandOptions.Server();

        var command = new Command("results", "Open the run's results on the web, or print them as JSON.") { runId, json, server };
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
                        if (parsed.GetValue(json))
                        {
                            await client.WaitForEndAsync(id, cancellationToken);
                            var results = await client.ResultsAsync(id, cancellationToken);
                            await output.WriteLineAsync(OutputJson.Of(results));
                            return RunVerdict.ExitCode(results);
                        }
                        await client.GetAsync(id, cancellationToken);
                        await WebPage.ShowAsync(ServerAddress.RunPage(site, id), output, parsed.InvocationConfiguration.Error, openBrowser);
                        return 0;
                    }
                )
        );
        return command;
    }
}
