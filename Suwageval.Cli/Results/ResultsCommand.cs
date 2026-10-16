using System.CommandLine;
using System.ComponentModel;
using System.Diagnostics;
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
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };

        var command = new Command("results", "Open the run's results on the web, or print them as JSON.") { runId, json, server };
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var output = parsed.InvocationConfiguration.Output;
                var site = ServerAddress.Site(parsed.GetValue(server));
                var transport = http ?? new HttpClient { BaseAddress = ServerAddress.Api(site) };
                var client = new RunClient(transport);
                var id = parsed.GetValue(runId);
                try
                {
                    if (parsed.GetValue(json))
                    {
                        await client.WaitForEndAsync(id, cancellationToken);
                        var results = await client.ResultsAsync(id, cancellationToken);
                        await output.WriteLineAsync(OutputJson.Of(results));
                        return RunVerdict.ExitCode(results);
                    }
                    await client.GetAsync(id, cancellationToken);
                    var page = ServerAddress.RunPage(site, id);
                    await output.WriteLineAsync(page.ToString());
                    (openBrowser ?? OpenInDefaultBrowser)(page);
                    return 0;
                }
                catch (Win32Exception)
                {
                    // No browser here (an SSH session, CI): the link above is the way in.
                    await parsed.InvocationConfiguration.Error.WriteLineAsync("No browser could be opened: open the link above.");
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

    // With the shell, .NET hands a URL to the system's opener (xdg-open, open, start).
    private static void OpenInDefaultBrowser(Uri page) =>
        Process.Start(new ProcessStartInfo(page.ToString()) { UseShellExecute = true })?.Dispose();
}
