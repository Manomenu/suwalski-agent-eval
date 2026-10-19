using System.CommandLine;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Compare;

/// <summary>
/// <c>suwageval compare &lt;runId&gt; &lt;runId&gt; [...]</c>: prints the comparison of 2 to 5 runs of one agent on stdout (the first run is the
/// baseline): a header per run, the pass rate per tag, the checks that differ; <c>--json</c> prints the comparison as JSON. <c>--open</c>
/// instead opens the comparison on the web (the link is printed too), after asking for every run first. A run that does not exist, runs
/// of different agents or a web that does not answer exit 2, and a wrong number of runs is refused before any request.
/// </summary>
internal static class CompareCommand
{
    // The same limits as the web page (Suwageval.Web/src/agents/view.ts).
    private const int MinRuns = 2;
    private const int MaxRuns = 5;

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    /// <param name="openBrowser">Opens a page; none means the system's default browser (the tests record the page instead).</param>
    public static Command Create(HttpClient? http, Action<Uri>? openBrowser)
    {
        var runIds = new Argument<Guid[]>("runId")
        {
            Description = $"{MinRuns} to {MaxRuns} runs of one agent, as `suwageval run` printed them; the first is the baseline.",
            Arity = new ArgumentArity(MinRuns, MaxRuns),
            CustomParser = result =>
            {
                var ids = CommandOptions.ParseIds(result, "run");
                var distinct = ids.Distinct().ToArray();
                if (ids.Count > 0 && distinct.Length < MinRuns)
                {
                    result.AddError($"Give {MinRuns} to {MaxRuns} different runs.");
                }
                return distinct;
            },
        };
        var json = CommandOptions.Json("Print the comparison as JSON.");
        var open = new Option<bool>("--open") { Description = "Open the comparison on the web instead of printing it." };
        var server = CommandOptions.Server();

        var command = new Command("compare", "Print the comparison of 2 to 5 runs of one agent (or --open it on the web).")
        {
            runIds,
            json,
            open,
            server,
        };
        command.Validators.Add(result =>
        {
            if (result.GetValue(json) && result.GetValue(open))
            {
                result.AddError("--json and --open cannot be used together.");
            }
        });
        command.SetAction(
            (parsed, cancellationToken) =>
            {
                var error = parsed.InvocationConfiguration.Error;
                return ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    error,
                    async (client, site) =>
                    {
                        var ids = parsed.GetRequiredValue(runIds);
                        if (!parsed.GetValue(open))
                        {
                            var comparison = await client.CompareAsync(ids, cancellationToken);
                            await parsed.InvocationConfiguration.Output.WriteLineAsync(
                                parsed.GetValue(json)
                                    ? OutputJson.Of(comparison)
                                    : CompareText.Describe(comparison, ServerAddress.ComparePage(site, ids))
                            );
                            return 0;
                        }
                        var agents = new List<string>();
                        foreach (var id in ids)
                        {
                            agents.Add((await client.GetAsync(id, cancellationToken)).Agent);
                        }
                        if (agents.Distinct().Count() > 1)
                        {
                            await error.WriteLineAsync(
                                $"Runs of different agents cannot be compared: {string.Join(", ", ids.Zip(agents, (id, agent) => $"{id} is of {agent}"))}."
                            );
                            return 2;
                        }
                        await WebPage.ShowAsync(
                            ServerAddress.ComparePage(site, ids),
                            parsed.InvocationConfiguration.Output,
                            error,
                            openBrowser
                        );
                        return 0;
                    }
                );
            }
        );
        return command;
    }
}
