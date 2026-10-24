using System.CommandLine;
using Suwageval.Cli.Server;

namespace Suwageval.Cli.Evaluation.Scenarios;

/// <summary>
/// <c>suwageval scenario from-conversation &lt;id&gt;</c>: a monitored conversation drafted as a scenario file, taken as it is (the agent
/// answers its last message), with the checks drafted from what the agent did as comments. Prints the YAML; with <c>--write</c> writes it
/// to <c>&lt;dir&gt;/scenarios/&lt;key&gt;.yaml</c>, never over a file that is there. Exits 0; 1 when the file exists; 2 when the server is wrong.
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
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command(
            "from-conversation",
            "Draft a scenario from a monitored conversation, as it is: the agent answers its last message; checks drafted as comments."
        )
        {
            id,
            write,
            server,
            dir,
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.RunAsync(
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, _) =>
                    {
                        var draft = await client.ScenarioDraftAsync(parsed.GetValue(id), cancellationToken);
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
                        await output.WriteLineAsync(
                            $"wrote {file}: review it (the checks are drafts, as comments), then validate and push."
                        );
                        return 0;
                    },
                    agentDir: parsed.GetRequiredValue(dir)
                )
        );
        return command;
    }
}
