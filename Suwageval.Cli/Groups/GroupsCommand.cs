using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Groups;

/// <summary>
/// <c>suwageval groups archive|unarchive &lt;name&gt;...</c> hides or restores run groups of the agent named in <c>suwageval/agent.yaml</c>
/// (<c>runs --groups</c> lists them); <c>groups delete &lt;name&gt;</c> deletes an archived group with its runs (after the person types its name, or
/// with <c>--yes</c>). Exits 0, 1 when a deletion is not confirmed, or 2 when the agent file, the server or a group is wrong.
/// </summary>
internal static class GroupsCommand
{
    /// <param name="terminal">Where <c>delete</c> asks the person to type the group's name.</param>
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(Terminal terminal, HttpClient? http = null) =>
        new("groups", "Archive or unarchive the agent's run groups; delete an archived one with its runs.")
        {
            Change("archive", "Archive run groups: hidden from the web's Groups section, kept with their runs.", archive: true, http),
            Change("unarchive", "Restore archived run groups.", archive: false, http),
            Delete(terminal, http),
        };

    private static Command Change(string name, string description, bool archive, HttpClient? http)
    {
        var names = new Argument<string[]>("name")
        {
            Description = "The groups, as `runs --groups` lists them.",
            Arity = ArgumentArity.OneOrMore,
        };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command(name, description) { names, server, dir };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var groupNames = parsed.GetRequiredValue(names);
                        await client.ArchiveRunGroupsAsync(agent, new ArchiveRunGroupsRequest(groupNames, archive), cancellationToken);
                        await parsed.InvocationConfiguration.Output.WriteLineAsync(
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{(archive ? "archived" : "unarchived")} {groupNames.Length} group(s)"
                            )
                        );
                        return 0;
                    }
                )
        );
        return command;
    }

    private static Command Delete(Terminal terminal, HttpClient? http)
    {
        var name = new Argument<string>("name") { Description = "The archived group to delete, as `runs --groups --archived` lists it." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Only print what would be deleted." };
        var yes = new Option<bool>("--yes") { Description = "Delete without asking (required when there is no terminal to ask on)." };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command(
            "delete",
            "Delete an archived run group with everything that belongs to it: its runs, their samples and verdicts. Cannot be undone. Exits 1 when not confirmed."
        )
        {
            name,
            dryRun,
            yes,
            server,
            dir,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var groupName = parsed.GetRequiredValue(name);
                        var output = parsed.InvocationConfiguration.Output;
                        var error = parsed.InvocationConfiguration.Error;
                        var contents = await client.RunGroupDeletionAsync(agent, groupName, cancellationToken);
                        await output.WriteLineAsync(GroupDeleteText.Preview(contents));
                        if (parsed.GetValue(dryRun))
                        {
                            return 0;
                        }
                        if (!parsed.GetValue(yes))
                        {
                            if (!terminal.Interactive)
                            {
                                await error.WriteLineAsync("Not deleted: there is no terminal to confirm on. Pass --yes to delete.");
                                return 1;
                            }
                            await output.WriteAsync("Type the group name to confirm: ");
                            if (!GroupDeleteText.Confirms(groupName, await terminal.Input.ReadLineAsync(cancellationToken)))
                            {
                                await error.WriteLineAsync("Not deleted: that is not the group name.");
                                return 1;
                            }
                        }
                        await client.DeleteRunGroupAsync(agent, groupName, cancellationToken);
                        await output.WriteLineAsync($"deleted group {groupName}");
                        return 0;
                    }
                )
        );
        return command;
    }
}
