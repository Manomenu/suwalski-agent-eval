using System.CommandLine;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Results;

/// <summary>
/// <c>suwageval results &lt;runId&gt;</c>: waits for the run to end and prints its results on stdout as text (status, version, tags, branch,
/// what it was compared with and the regressions, failing checks with their reasons, turn times, the link to its page); <c>--json</c>
/// prints the results as JSON instead (all of them: <c>--failed-only</c> narrows the text only). Both exit like <c>run --wait --strict</c> (0 every check passed, 1 one did not, 2 the run did not
/// finish). <c>--open</c> instead opens the run's page on the web — the link is printed too, for a terminal with no browser — and exits 0;
/// with <c>--print</c> the page also opens the browser's print dialog (for a person; <c>--print</c> without <c>--open</c> exits 2).
/// Either way a run that does not exist or a web that does not answer exits 2.
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
            Description = "Print the results as JSON instead of text; waits for the run to end and exits by its checks.",
        };
        var open = new Option<bool>("--open") { Description = "Open the run's page on the web instead of printing the results." };
        var print = new Option<bool>("--print")
        {
            Description =
                "With --open: the page opens the browser's print dialog (A4 landscape; \"Save as PDF\" is in it). For a person: nothing is printed without one.",
        };
        var failedOnly = new Option<bool>("--failed-only")
        {
            Description =
                "Print only what did not pass (failed checks, checks not judged, samples with an agent error) and how many passed ones were left out. With --json nothing is left out.",
        };
        var server = CommandOptions.Server();

        var command = new Command("results", "Print the run's results (or --open its page on the web).")
        {
            runId,
            json,
            open,
            print,
            failedOnly,
            server,
        };
        command.Validators.Add(result =>
        {
            if (result.GetValue(json) && result.GetValue(open))
            {
                result.AddError("--json and --open cannot be used together.");
            }
            if (result.GetValue(failedOnly) && result.GetValue(open))
            {
                result.AddError("--failed-only and --open cannot be used together.");
            }
        });
        command.SetAction(
            (parsed, cancellationToken) =>
            {
                if (parsed.GetValue(print) && !parsed.GetValue(open))
                {
                    // Exit 2 like every refusal of a command that is understood but cannot be done.
                    return CommandOptions.RefuseAsync(
                        parsed.InvocationConfiguration.Error,
                        "--print needs --open: it opens the page's print dialog."
                    );
                }
                return ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, site) =>
                    {
                        var output = parsed.InvocationConfiguration.Output;
                        var id = parsed.GetRequiredValue(runId);
                        if (parsed.GetValue(open))
                        {
                            await client.GetAsync(id, cancellationToken);
                            await WebPage.ShowAsync(
                                ServerAddress.RunPage(site, id, parsed.GetValue(print)),
                                output,
                                parsed.InvocationConfiguration.Error,
                                openBrowser
                            );
                            return 0;
                        }
                        await client.WaitForEndAsync(id, cancellationToken);
                        var results = await client.ResultsAsync(id, cancellationToken);
                        await output.WriteLineAsync(
                            parsed.GetValue(json)
                                ? OutputJson.Of(results)
                                : ResultsText.Describe(results, ServerAddress.RunPage(site, id), parsed.GetValue(failedOnly))
                        );
                        return RunVerdict.ExitCode(results, strict: true);
                    }
                );
            }
        );
        return command;
    }
}
