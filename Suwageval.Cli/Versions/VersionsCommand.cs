using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>
/// <c>suwageval versions</c>: lists the versions of the agent named in <c>suwageval/agent.yaml</c> (the active ones; with
/// <c>--archived</c> the archived ones too), and <c>versions archive|unarchive &lt;versionId&gt;...</c> hides or restores them.
/// <c>versions diff &lt;a&gt; &lt;b&gt;</c> opens the page showing what changed between two versions. A version is never deleted — every run points at one. Exits 0, or 2 when the agent file, the server or a version is wrong.
/// </summary>
internal static class VersionsCommand
{
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    /// <param name="openBrowser">Opens a page for <c>diff</c>; none means the system's default browser (the tests record the page instead).</param>
    public static Command Create(HttpClient? http = null, Action<Uri>? openBrowser = null)
    {
        var archived = new Option<bool>("--archived") { Description = "Also list the archived versions (marked archived)." };
        var json = CommandOptions.Json("Print the versions as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command("versions", "List the agent's versions; archive or unarchive them. A version is never deleted.")
        {
            archived,
            json,
            server,
            dir,
            Change("archive", "Archive versions: hidden from the web's default views, kept with their runs.", archive: true, http),
            Change("unarchive", "Restore archived versions.", archive: false, http),
            Diff(http, openBrowser),
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
                        var versions = (await client.VersionsAsync(agent, cancellationToken))
                            .Where(version => parsed.GetValue(archived) || version.ArchivedAt is null)
                            .ToList();
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(versions));
                            return 0;
                        }
                        foreach (var version in versions)
                        {
                            await output.WriteLineAsync(VersionList.Line(version));
                        }
                        return 0;
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
                return [.. CommandOptions.ParseIds(result, "version")];
            },
        };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command(name, description) { ids, server, dir };
        command.SetAction(
            (parsed, cancellationToken) =>
                ServerCall.ForAgentAsync(
                    parsed.GetRequiredValue(dir),
                    parsed.GetValue(server),
                    http,
                    parsed.InvocationConfiguration.Error,
                    async (client, agent, _) =>
                    {
                        var versionIds = parsed.GetRequiredValue(ids);
                        await client.ArchiveVersionsAsync(agent, new ArchiveVersionsRequest(versionIds, archive), cancellationToken);
                        await parsed.InvocationConfiguration.Output.WriteLineAsync(
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{(archive ? "archived" : "unarchived")} {versionIds.Length} version(s)"
                            )
                        );
                        return 0;
                    }
                )
        );
        return command;
    }

    private static Command Diff(HttpClient? http, Action<Uri>? openBrowser)
    {
        var first = new Argument<Guid>("versionA") { Description = "One version of the agent, as `versions` lists it." };
        var second = new Argument<Guid>("versionB") { Description = "The other version; the older one of the two is the base." };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("diff", "Open the page showing what changed between two versions of the agent on the web.")
        {
            first,
            second,
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
                    async (client, agent, site) =>
                    {
                        var a = parsed.GetRequiredValue(first);
                        var b = parsed.GetRequiredValue(second);
                        if (a == b)
                        {
                            throw new ServerClientException("Give two different versions.");
                        }
                        await client.VersionAsync(agent, a, cancellationToken);
                        await client.VersionAsync(agent, b, cancellationToken);
                        await WebPage.ShowAsync(
                            ServerAddress.VersionComparePage(site, agent, a, b),
                            parsed.InvocationConfiguration.Output,
                            parsed.InvocationConfiguration.Error,
                            openBrowser
                        );
                        return 0;
                    }
                )
        );
        return command;
    }
}
