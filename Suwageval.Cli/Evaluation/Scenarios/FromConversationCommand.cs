using System.CommandLine;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Evaluation.Scenarios;

/// <summary>
/// <c>suwageval scenario from-conversation &lt;id&gt;</c>: a monitored conversation drafted as a scenario file, copied to its end or, with
/// <c>--up-to-user N</c> / <c>--up-to-agent N</c>, up to its N-th user or agent message (included); the monitoring checks it failed come
/// below as comments. With <c>--simulate</c> (and a cut) the rest is simulated: a model describes the user from the whole conversation,
/// for <c>--turns</c> turns (by default as many as the real conversation had after the cut). Prints the YAML; with <c>--write</c> writes it to <c>&lt;dir&gt;/scenarios/&lt;key&gt;.yaml</c>, never over a file that
/// is there. Exits 0; 1 when the file exists; 2 when the server is wrong (no such message too).
/// </summary>
internal static class FromConversationCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var id = new Argument<Guid>("id") { Description = "The conversation's id, as `monitoring conversations` lists it." };
        var write = new Option<bool>("--write")
        {
            Description = "Write the file to <dir>/scenarios/<key>.yaml instead of printing it (never over an existing file).",
        };
        var upToUser = MessageOption("--up-to-user", "user");
        var upToAgent = MessageOption("--up-to-agent", "agent");
        var simulate = new Option<bool>("--simulate")
        {
            Description =
                "Simulate the rest after the cut: a model describes the user from the whole conversation (Models:describer, else Models:fallback).",
        };
        var turns = new Option<int?>("--turns")
        {
            Description =
                "How many turns the simulated rest runs, 1 to 20 (with --simulate); by default as many as the real conversation had after the cut.",
        };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command(
            "from-conversation",
            "Draft a scenario from a monitored conversation, copied to its end or up to a user or agent message, the rest optionally simulated; its failed monitoring checks as comments."
        )
        {
            id,
            upToUser,
            upToAgent,
            simulate,
            turns,
            write,
            server,
            dir,
        };
        command.Validators.Add(result =>
        {
            if (result.GetValue(upToUser) is not null && result.GetValue(upToAgent) is not null)
            {
                result.AddError("Copy up to a user message or an agent message, not both.");
            }
            if (result.GetValue(simulate) && result.GetValue(upToUser) is null && result.GetValue(upToAgent) is null)
            {
                result.AddError("--simulate needs --up-to-user or --up-to-agent: the rest after that message is simulated.");
            }
            if (result.GetValue(turns) is not null && !result.GetValue(simulate))
            {
                result.AddError("--turns needs --simulate.");
            }
        });
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, _) =>
                    {
                        var draft = await client.ScenarioDraftAsync(
                            parsed.GetValue(id),
                            parsed.GetValue(upToUser),
                            parsed.GetValue(upToAgent),
                            parsed.GetValue(simulate),
                            parsed.GetValue(turns),
                            cancellationToken
                        );
                        if (draft.Notice is { } notice)
                        {
                            await parsed.InvocationConfiguration.Error.WriteLineAsync(notice);
                        }
                        var output = parsed.InvocationConfiguration.Output;
                        if (!parsed.GetValue(write))
                        {
                            await output.WriteAsync(draft.Yaml);
                            await parsed.InvocationConfiguration.Error.WriteLineAsync(
                                $"Review it, then save it as {draft.Path} (or run again with --write), validate and push."
                            );
                            return 0;
                        }
                        var file = Path.Combine(parsed.GetRequiredValue(dir), "scenarios", $"{draft.Key}.yaml");
                        if (File.Exists(file))
                        {
                            await parsed.InvocationConfiguration.Error.WriteLineAsync(
                                $"{file} exists: nothing written. Rename the key in the draft, or remove the file first."
                            );
                            return 1;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                        await File.WriteAllTextAsync(file, draft.Yaml, cancellationToken);
                        await output.WriteLineAsync($"wrote {file}: review it, add the checks it must keep, then validate and push.");
                        return 0;
                    },
                    agentDir: parsed.GetRequiredValue(dir)
                )
        );
        return command;
    }

    /// <summary>The option naming the <paramref name="who"/> message (from 1) the copy ends at.</summary>
    private static Option<int?> MessageOption(string name, string who)
    {
        var option = new Option<int?>(name) { Description = $"Copy up to the conversation's N-th {who} message (from 1), included." };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is < 1)
            {
                result.AddError($"{name} is a {who} message of the conversation, from 1.");
            }
        });
        return option;
    }
}
