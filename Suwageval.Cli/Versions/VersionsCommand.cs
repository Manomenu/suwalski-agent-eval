using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Files;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>
/// <c>suwageval versions</c>: lists the versions of the agent named in <c>suwageval/agent.yaml</c> (the active ones; with
/// <c>--archived</c> the archived ones too), and <c>versions archive|unarchive &lt;versionId&gt;...</c> hides or restores them.
/// A version is never deleted — every run points at one. Exits 0, or 2 when the agent file, the server or a version is wrong.
/// </summary>
internal static class VersionsCommand
{
    private const string DefaultDir = "suwageval";

    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    public static Command Create(HttpClient? http = null)
    {
        var archived = new Option<bool>("--archived") { Description = "Also list the archived versions (marked archived)." };
        var json = new Option<bool>("--json") { Description = "Print the versions as JSON." };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };
        var dir = new Option<string>("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => DefaultDir };

        var command = new Command("versions", "List the agent's versions; archive or unarchive them. A version is never deleted.")
        {
            archived,
            json,
            server,
            dir,
            Change("archive", "Archive versions: hidden from the web's default views, kept with their runs.", archive: true, http),
            Change("unarchive", "Restore archived versions.", archive: false, http),
        };
        command.SetAction(
            (parsed, cancellationToken) =>
                WithClient(
                    parsed,
                    parsed.GetValue(server),
                    parsed.GetRequiredValue(dir),
                    http,
                    async (client, agent) =>
                    {
                        var versions = (await client.VersionsAsync(agent, cancellationToken))
                            .Where(version => parsed.GetValue(archived) || version.ArchivedAt is null)
                            .ToList();
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(versions));
                            return;
                        }
                        foreach (var version in versions)
                        {
                            await output.WriteLineAsync(VersionList.Line(version));
                        }
                    }
                )
        );
        return command;
    }

    private static Command Change(string name, string description, bool archive, HttpClient? http)
    {
        var ids = new Argument<Guid[]>("versionId")
        {
            Description = "The versions, as `versions` lists them.",
            Arity = ArgumentArity.OneOrMore,
            CustomParser = result =>
            {
                var parsedIds = new List<Guid>();
                foreach (var token in result.Tokens)
                {
                    if (Guid.TryParse(token.Value, out var id))
                    {
                        parsedIds.Add(id);
                    }
                    else
                    {
                        result.AddError($"'{token.Value}' is not a version id.");
                    }
                }
                return [.. parsedIds];
            },
        };
        var server = new Option<string?>("--server") { Description = ServerAddress.OptionDescription };
        var dir = new Option<string>("--dir") { Description = "The folder with agent.yaml.", DefaultValueFactory = _ => DefaultDir };
        var command = new Command(name, description) { ids, server, dir };
        command.SetAction(
            (parsed, cancellationToken) =>
                WithClient(
                    parsed,
                    parsed.GetValue(server),
                    parsed.GetRequiredValue(dir),
                    http,
                    async (client, agent) =>
                    {
                        var versionIds = parsed.GetRequiredValue(ids);
                        await client.ArchiveVersionsAsync(agent, new ArchiveVersionsRequest(versionIds, archive), cancellationToken);
                        await parsed.InvocationConfiguration.Output.WriteLineAsync(
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{(archive ? "archived" : "unarchived")} {versionIds.Length} version(s)"
                            )
                        );
                    }
                )
        );
        return command;
    }

    /// <summary>Reads the agent's name, makes the client and runs <paramref name="action"/>; 0, or 2 with the message on stderr.</summary>
    private static async Task<int> WithClient(
        ParseResult parsed,
        string? serverOption,
        string dir,
        HttpClient? http,
        Func<RunClient, string, Task> action
    )
    {
        var error = parsed.InvocationConfiguration.Error;
        var agent = AgentName.Read(dir, error);
        if (agent is null)
        {
            return 2;
        }
        HttpClient? owned = null;
        try
        {
            var transport = http ?? (owned = new HttpClient { BaseAddress = ServerAddress.Api(ServerAddress.Site(serverOption)) });
            await action(new RunClient(transport), agent);
            return 0;
        }
        catch (RunClientException failure)
        {
            await error.WriteLineAsync(failure.Message);
            return 2;
        }
        finally
        {
            owned?.Dispose();
        }
    }
}
