using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Compare;

/// <summary>
/// <c>suwageval compare &lt;runId&gt;…</c> or <c>--group &lt;name&gt;</c>: prints the runs of one agent side by side — a column per run
/// (its version's label and models, checks passed, samples answered, turn times), the pass rate per tag and a cell per scenario with the
/// failed checks. With <c>--baseline &lt;runId&gt;</c> (one of the runs) it also prints how the others stand against that run: changes in
/// pass rates and turn times, the judge, the checks that differ. <c>--json</c> prints the record, <c>--open</c> opens the same view on the
/// web. A group is looked up for the agent of <c>suwageval/agent.yaml</c>. An unknown run or group, runs of different agents and a web
/// that does not answer exit 2.
/// </summary>
internal static class CompareCommand
{
    // The same limit as the server's (RunComparisonReport.MaxRuns) and the web page (Suwageval.Web/src/agents/view.ts).
    private const int MaxRuns = 20;

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    /// <param name="openBrowser">Opens a page; none means the system's default browser (the tests record the page instead).</param>
    public static Command Create(HttpClient? http, Action<Uri>? openBrowser)
    {
        var runIds = new Argument<Guid[]>("runId")
        {
            Description = $"Up to {MaxRuns} runs of one agent, as `suwageval run` printed them (or use --group).",
            Arity = new ArgumentArity(0, MaxRuns),
            CustomParser = result => [.. CommandOptions.ParseIds(result, "run").Distinct()],
        };
        var group = CommandOptions.Group("All the runs of this group (run --group) of the agent in agent.yaml, oldest first.");
        var baseline = new Option<Guid?>("--baseline")
        {
            Description = "One of the runs: also print how the others stand against it (changes in pass rates, turn times, judge, checks).",
            CustomParser = result =>
            {
                var ids = CommandOptions.ParseIds(result, "run");
                return ids.Count == 1 ? ids[0] : null;
            },
        };
        var json = CommandOptions.Json("Print the comparison as JSON.");
        var open = new Option<bool>("--open") { Description = "Open the comparison on the web instead of printing it." };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "compare",
            "Print runs of one agent side by side, optionally against a baseline run (or --open them on the web)."
        )
        {
            runIds,
            group,
            baseline,
            json,
            open,
            server,
            dir,
        };
        command.Validators.Add(result =>
        {
            // The tokens, not the parsed values: a token that is no id already is an error of its own, and reading its value would throw.
            var ids = (result.GetResult(runIds)?.Tokens ?? [])
                .Select(token => Guid.TryParse(token.Value, out var id) ? id : (Guid?)null)
                .Distinct()
                .ToArray();
            if ((ids.Length > 0) == (result.GetValue(group) is not null))
            {
                result.AddError("Give run ids or --group, not both and not neither.");
            }
            if (result.GetResult(baseline)?.Tokens is [{ } token] && Guid.TryParse(token.Value, out var baselineId))
            {
                if (ids.Length > 0 && !ids.Contains(baselineId))
                {
                    result.AddError("--baseline must be one of the runs given.");
                }
                if (ids.Length == 1)
                {
                    result.AddError("--baseline needs at least two runs to compare.");
                }
            }
            if (result.GetValue(json) && result.GetValue(open))
            {
                result.AddError("--json and --open cannot be used together.");
            }
        });
        command.SetAction(
            (parsed, cancellationToken) =>
            {
                var error = parsed.InvocationConfiguration.Error;
                var agentDir = parsed.GetRequiredValue(dir);
                var groupName = parsed.GetValue(group);
                var agent = groupName is null ? null : AgentReader.Read(agentDir, error)?.Definition.Name;
                if (groupName is not null && agent is null)
                {
                    return Task.FromResult(2);
                }
                return ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    error,
                    async (client, site) =>
                    {
                        var ids = parsed.GetValue(runIds) ?? [];
                        var baselineId = parsed.GetValue(baseline);
                        var comparison = await client.CompareAsync(ids, groupName, agent, baselineId, cancellationToken);
                        var page = ServerAddress.ComparePage(site, ids, groupName, agent, baselineId);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(open))
                        {
                            await WebPage.ShowAsync(page, output, error, openBrowser);
                            return 0;
                        }
                        await output.WriteLineAsync(
                            parsed.GetValue(json) ? OutputJson.Of(comparison) : CompareText.Describe(comparison, page)
                        );
                        return 0;
                    },
                    agentDir: agentDir
                );
            }
        );
        return command;
    }
}
