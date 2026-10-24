using System.CommandLine;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Evaluation.Note;

/// <summary>
/// <c>suwageval note &lt;runId&gt; &lt;text&gt;</c> sets the note of a run (why it was started), <c>--clear</c> removes it. Works for a run in
/// any status and changes no verdict. Prints the run's page; exits 0, 2 for an unknown run or a server that does not answer, and
/// refuses a note the rules (<see cref="NoteRules"/>) do not allow before calling the server.
/// <c>suwageval note --group &lt;name&gt; &lt;text&gt;</c> (or <c>--clear</c>) sets the note of a run group of the agent of
/// <c>suwageval/agent.yaml</c> instead (what the experiment is about; the comparison of the group shows it); a run id and <c>--group</c>
/// exclude each other. Prints the group's compare page.
/// </summary>
internal static class NoteCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        // Both are plain words for the parser: with --group the one argument is the text, which then lands in the first.
        var runId = new Argument<string?>("runId")
        {
            Description = "The run, as `suwageval run` printed it (with --group there is no run id: give only the text).",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var text = new Argument<string?>("text")
        {
            Description = $"The new note, free text (at most {NoteRules.MaxLength} characters).",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var group = CommandOptions.Group(
            "Set the note of this run group of the agent in agent.yaml (shown in its comparison) instead of a run's."
        );
        var dir = CommandOptions.Dir();
        var clear = new Option<bool>("--clear") { Description = "Remove the note instead of setting one." };
        var server = CommandOptions.Server();

        var command = new Command(
            "note",
            "Set or clear the note of a run (why it was started) or, with --group, of a run group. It changes no verdict."
        )
        {
            runId,
            text,
            clear,
            group,
            server,
            dir,
        };
        command.Validators.Add(result =>
        {
            var grouped = result.GetValue(group) is not null;
            if (grouped && RunGroupRules.Problem(result.GetValue(group)) is { } groupProblem)
            {
                result.AddError(groupProblem);
                return;
            }
            if (grouped && result.GetValue(text) is not null)
            {
                result.AddError("With --group give only the text (no run id).");
                return;
            }
            if (!grouped && (result.GetValue(runId) is not { } first || !Guid.TryParse(first, out _)))
            {
                result.AddError("Give the run id (or --group).");
                return;
            }
            var given = grouped ? result.GetValue(runId) : result.GetValue(text);
            if (result.GetValue(clear) == !string.IsNullOrWhiteSpace(given))
            {
                result.AddError("Give the note's text or --clear, not both and not neither.");
            }
            else if (NoteRules.Problem(given) is { } problem)
            {
                result.AddError(problem);
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
                        var output = parsed.InvocationConfiguration.Output;
                        var cleared = parsed.GetValue(clear);
                        if (groupName is not null)
                        {
                            await client.SetGroupNoteAsync(agent!, groupName, cleared ? null : parsed.GetValue(runId), cancellationToken);
                            await output.WriteLineAsync(
                                cleared ? $"Cleared the note of group {groupName}." : $"Set the note of group {groupName}."
                            );
                            await output.WriteLineAsync(ServerAddress.ComparePage(site, [], groupName, agent, null).ToString());
                            return 0;
                        }
                        var id = Guid.Parse(parsed.GetRequiredValue(runId)!);
                        await client.SetNoteAsync(id, cleared ? null : parsed.GetValue(text), cancellationToken);
                        await output.WriteLineAsync(cleared ? $"Cleared the note of run {id}." : $"Set the note of run {id}.");
                        await output.WriteLineAsync(ServerAddress.RunPage(site, id).ToString());
                        return 0;
                    },
                    agentDir: agentDir
                );
            }
        );
        return command;
    }
}
