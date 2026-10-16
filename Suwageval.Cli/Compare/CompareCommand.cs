using System.CommandLine;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Compare;

/// <summary>
/// <c>suwageval compare &lt;runId&gt; &lt;runId&gt; [...]</c>: opens the comparison of 2 to 5 runs of one agent on the web (the first run
/// is the baseline) and prints the link too. Every run is asked for first through the web's address, so a run that does not exist, runs
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
                var ids = new List<Guid>();
                foreach (var token in result.Tokens)
                {
                    if (Guid.TryParse(token.Value, out var id))
                    {
                        ids.Add(id);
                    }
                    else
                    {
                        result.AddError($"'{token.Value}' is not a run id.");
                    }
                }
                var distinct = ids.Distinct().ToArray();
                if (ids.Count > 0 && distinct.Length < MinRuns)
                {
                    result.AddError($"Give {MinRuns} to {MaxRuns} different runs.");
                }
                return distinct;
            },
        };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };

        var command = new Command("compare", "Open the comparison of 2 to 5 runs of one agent on the web.") { runIds, server };
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var site = ServerAddress.Site(parsed.GetValue(server));
                var transport = http ?? new HttpClient { BaseAddress = ServerAddress.Api(site) };
                var client = new RunClient(transport);
                var ids = parsed.GetRequiredValue(runIds);
                var error = parsed.InvocationConfiguration.Error;
                try
                {
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
                catch (RunClientException failure)
                {
                    await error.WriteLineAsync(failure.Message);
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
