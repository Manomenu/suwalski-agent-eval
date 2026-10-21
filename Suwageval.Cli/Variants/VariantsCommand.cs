using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Variants;

/// <summary>
/// <c>suwageval variants &lt;runId&gt;…</c> or <c>--group &lt;name&gt;</c>: prints the runs of one agent as variants — a column per run
/// (its version's label and models, checks passed, samples answered, turn times), the pass rate per tag and a cell per scenario with the
/// failed checks; <c>--json</c> prints the records, <c>--open</c> opens the same view on the web. A group is looked up for the agent of
/// <c>suwageval/agent.yaml</c>. An unknown run or group, runs of different agents and a web that does not answer exit 2.
/// </summary>
internal static class VariantsCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    /// <param name="openBrowser">Opens a page; none means the system's default browser (the tests record the page instead).</param>
    public static Command Create(HttpClient? http, Action<Uri>? openBrowser)
    {
        var runIds = new Argument<Guid[]>("runId")
        {
            Description = "Runs of one agent, as `suwageval run` printed them (or use --group).",
            Arity = ArgumentArity.ZeroOrMore,
            CustomParser = result => [.. CommandOptions.ParseIds(result, "run").Distinct()],
        };
        var group = CommandOptions.Group("All the runs of this group (run --group) of the agent in agent.yaml, oldest first.");
        var json = CommandOptions.Json("Print the variants as JSON.");
        var open = new Option<bool>("--open") { Description = "Open the variants on the web instead of printing them." };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command("variants", "Print runs of one agent side by side as variants (or --open them on the web).")
        {
            runIds,
            group,
            json,
            open,
            server,
            dir,
        };
        command.Validators.Add(result =>
        {
            if ((result.GetValue(runIds)?.Length > 0) == (result.GetValue(group) is not null))
            {
                result.AddError("Give run ids or --group, not both and not neither.");
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
                        var variants = await client.VariantsAsync(ids, groupName, agent, cancellationToken);
                        var page = ServerAddress.VariantsPage(site, ids, groupName, agent);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(open))
                        {
                            await WebPage.ShowAsync(page, output, error, openBrowser);
                            return 0;
                        }
                        await output.WriteLineAsync(
                            parsed.GetValue(json) ? OutputJson.Of(variants) : VariantsText.Describe(variants, page)
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
