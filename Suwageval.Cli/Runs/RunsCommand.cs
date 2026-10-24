using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Runs;

/// <summary>
/// <c>suwageval runs</c>: the runs of the agent named in <c>suwageval/agent.yaml</c>, newest first — id, status, tags, version, checks
/// passed of all, creation time, branch and a "reference" mark; <c>--limit</c> keeps the newest ones (default 10), <c>--version</c> (repeatable)
/// those of the given agent versions, <c>--reference</c> only reference runs, <c>--tag</c> (repeatable) only the runs that ran the scenarios of any of the tags (limited to tags including one, or not limited), <c>--group</c> only the runs of a group, <c>--any-group</c> only the runs in some group, <c>--all-runs</c> grouped or not (the default is the runs in no group,
/// like the web: group runs are experiments), <c>--groups</c> lists the groups instead (<c>--archived</c>: the archived ones too), <c>--json</c> prints the run summaries. It is how a
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
        var tag = new Option<string[]>("--tag")
        {
            Description =
                "Only the runs that ran the scenarios of this tag: limited to tags including it, or not limited at all (repeat it for several: any of them).",
            AllowMultipleArgumentsPerToken = false,
        };
        var group = CommandOptions.Group("Only the runs started in this group (run --group).");
        var anyGroup = new Option<bool>("--any-group") { Description = "Only the runs started in some group." };
        var allRuns = new Option<bool>("--all-runs")
        {
            Description = "The runs in a group and those in none (the default is the runs in no group).",
        };
        var archived = new Option<bool>("--archived") { Description = "With --groups: the archived groups too (marked archived)." };
        var groups = new Option<bool>("--groups")
        {
            Description = "List the agent's run groups (name, runs, versions, newest run) instead of runs.",
        };
        var json = CommandOptions.Json("Print the runs (or the groups) as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command(
            "runs",
            "List the agent's newest runs: those in no group unless --group, --any-group or --all-runs says otherwise."
        )
        {
            limit,
            version,
            reference,
            tag,
            group,
            anyGroup,
            allRuns,
            groups,
            archived,
            json,
            server,
            dir,
        };
        command.Validators.Add(result =>
        {
            if (
                new[] { result.GetValue(group) is not null, result.GetValue(anyGroup), result.GetValue(allRuns) }.Count(chosen => chosen)
                > 1
            )
            {
                result.AddError("--group, --any-group and --all-runs cannot be used together.");
            }
        });
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
                            var listed = (await client.RunGroupsAsync(agent, cancellationToken))
                                .Where(runGroup => parsed.GetValue(archived) || runGroup.ArchivedAt is null)
                                .ToList();
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
                                        $"{runGroup.Name}  {runGroup.Runs} runs  {runGroup.Versions} versions  newest {runGroup.NewestRunAt:yyyy-MM-dd HH:mm}Z{(runGroup.ArchivedAt is null ? "" : "  archived")}"
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
                            parsed.GetValue(group),
                            parsed.GetValue(anyGroup) ? RunGrouping.Any
                                : parsed.GetValue(allRuns) || parsed.GetValue(group) is not null ? RunGrouping.All
                                : RunGrouping.None,
                            parsed.GetValue(tag)
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
                                    $"{run.Id}  {run.Status}  {RunTagsText.Of(run.Tags)}  version {run.AgentVersionId}{RunLabelText.Suffix(run.Label)}  {run.ChecksPassed}/{run.ChecksPassed + run.ChecksFailed} checks passed  {run.CreatedAt:yyyy-MM-dd HH:mm}Z  branch {run.Origin?.Branch ?? "-"}{(run.Reference ? "  reference" : "")}{(run.Group is null ? "" : $"  group {run.Group}")}{(run.JudgeModels.Count > 0 ? $"  judge {string.Join(", ", run.JudgeModels)}" : "")}{RunNoteText.Suffix(run.Note)}"
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
