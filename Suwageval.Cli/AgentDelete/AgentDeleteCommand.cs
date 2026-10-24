using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Output;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.AgentDelete;

/// <summary>
/// <c>suwageval delete-agent</c>: deletes the agent named in <c>suwageval/agent.yaml</c> from the server — a registration made by mistake —
/// with its versions, scenarios, checks and every run with its samples and verdicts. Prints what goes first; the person then types the
/// agent's name (or passes <c>--yes</c>; <c>--dry-run</c> only prints). Files in the repository are not touched. Exits 0, 1 when not
/// confirmed, 2 when the agent file, the server or the agent is wrong (an unknown agent, or a run still queued or running).
/// </summary>
internal static class AgentDeleteCommand
{
    /// <param name="terminal">Where the person is asked to type the agent's name.</param>
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(Terminal terminal, HttpClient? http = null)
    {
        var dryRun = new Option<bool>("--dry-run") { Description = "Only print what would be deleted." };
        var yes = new Option<bool>("--yes") { Description = "Delete without asking (required when there is no terminal to ask on)." };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command(
            "delete-agent",
            "Delete the agent of this repository from the server with everything that belongs to it: versions, scenarios, checks, runs, samples and verdicts. Cannot be undone. Exits 1 when not confirmed."
        )
        {
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
                        var output = parsed.InvocationConfiguration.Output;
                        var error = parsed.InvocationConfiguration.Error;
                        var contents = await client.AgentDeletionAsync(agent, cancellationToken);
                        await output.WriteLineAsync(Preview(contents));
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
                            await output.WriteAsync("Type the agent's name to confirm: ");
                            if (await terminal.Input.ReadLineAsync(cancellationToken) != contents.Name)
                            {
                                await error.WriteLineAsync("Not deleted: that is not the agent's name.");
                                return 1;
                            }
                        }
                        await client.DeleteAgentAsync(agent, cancellationToken);
                        await output.WriteLineAsync($"deleted agent {contents.Name}");
                        return 0;
                    }
                )
        );
        return command;
    }

    private static string Preview(AgentDeletion contents) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"agent {contents.Name}\nwould delete: {contents.Versions} version(s), {contents.Scenarios} scenario(s), {contents.Checks} check(s), {contents.Runs} run(s), {contents.Samples} sample(s), {contents.Verdicts} verdict(s){(contents.Conversations > 0 ? $", {contents.Conversations} monitored conversation(s)" : "")}"
        );
}
