using System.CommandLine;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Evaluation.Results;

internal static class ResultsCommand
{
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
