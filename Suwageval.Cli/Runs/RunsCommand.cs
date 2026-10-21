using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Runs;

/// <summary>
/// <c>suwageval runs</c>: the runs of the agent named in <c>suwageval/agent.yaml</c>, newest first — id, status, tags, version, checks
/// passed of all, creation time, branch and a "reference" mark; <c>--limit</c> keeps the newest ones (default 10), <c>--version</c> (repeatable)
/// those of the given agent versions, <c>--reference</c> only reference runs, <c>--group</c> only the runs of a group, <c>--groups</c> lists the groups instead, <c>--json</c> prints the run summaries. It is how a
/// run to compare with is found (<c>suwageval compare</c>). Exits 0, or 2 when the agent file or the server is wrong.
/// </summary>
internal static class RunsCommand
{
    private const int DefaultLimit = 10;

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var limit = new Option<int>("--limit")
        {
            Description = "How many of the newest runs to list.",
            DefaultValueFactory = _ => DefaultLimit,
        };
        var version = new Option<Guid[]>("--version")
        {
            Description = "Only the runs of this agent version (repeat it for several).",
            AllowMultipleArgumentsPerToken = false,
        };
        var reference = new Option<bool>("--reference")
        {
            Description = "Only reference runs: the ones that make the history regressions are measured against.",
        };
        var group = CommandOptions.Group("Only the runs started in this group (run --group).");
        var groups = new Option<bool>("--groups")
        {
            Description = "List the agent's run groups (name, runs, versions, newest run) instead of runs.",
        };
        var json = CommandOptions.Json("Print the runs (or the groups) as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command("runs", "List the agent's newest runs.") { limit, version, reference, group, groups, json, server, dir };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        if (parsed.GetValue(groups))
                        {
                            var listed = await client.RunGroupsAsync(agent, cancellationToken);
                            var groupOutput = parsed.InvocationConfiguration.Output;
                            if (parsed.GetValue(json))
                            {
                                await groupOutput.WriteLineAsync(OutputJson.Of(listed));
                                return 0;
                            }
                            foreach (var runGroup in listed)
                            {
                                await groupOutput.WriteLineAsync(
                                    string.Create(
                                        CultureInfo.InvariantCulture,
                                        $"{runGroup.Name}  {runGroup.Runs} runs  {runGroup.Versions} versions  newest {runGroup.NewestRunAt:yyyy-MM-dd HH:mm}Z"
                                    )
                                );
                            }
                            return 0;
                        }
                        var runs = await client.RunsAsync(
                            agent,
                            parsed.GetValue(limit),
                            cancellationToken,
                            parsed.GetValue(version),
                            parsed.GetValue(reference),
                            parsed.GetValue(group)
                        );
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(runs));
                            return 0;
                        }
                        foreach (var run in runs)
                        {
                            await output.WriteLineAsync(
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"{run.Id}  {run.Status}  {RunTagsText.Of(run.Tags)}  version {run.AgentVersionId}{RunLabelText.Suffix(run.Label)}  {run.ChecksPassed}/{run.ChecksPassed + run.ChecksFailed} checks passed  {run.CreatedAt:yyyy-MM-dd HH:mm}Z  branch {run.Origin?.Branch ?? "-"}{(run.Reference ? "  reference" : "")}{(run.Group is null ? "" : $"  group {run.Group}")}{(run.JudgeModels.Count > 0 ? $"  judge {string.Join(", ", run.JudgeModels)}" : "")}"
                                )
                            );
                        }
                        return 0;
                    }
                )
        );
        return command;
    }
}
