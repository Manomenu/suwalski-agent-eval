using System.CommandLine;
using System.Globalization;
using Suwageval.Cli.Server;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>
/// <c>suwageval versions</c>: lists the versions of the agent named in <c>suwageval/agent.yaml</c> (the active ones; with
/// <c>--archived</c> the archived ones too), and <c>versions archive|unarchive &lt;versionId&gt;...</c> hides or restores them.
/// <c>versions diff &lt;a&gt; &lt;b&gt;</c> prints what changed between two versions as a unified-style diff (<c>--json</c> the record, <c>--open</c> the page on the web), <c>versions delete &lt;versionId&gt;</c> deletes an archived
/// version with its runs (after the person types its id, or with <c>--yes</c>). Exits 0, or 2 when the agent file, the server or a version is wrong.
/// </summary>
internal static class VersionsCommand
{
    /// <param name="terminal">Where <c>delete</c> asks the person to type the version's id.</param>
    /// <param name="http">The client to the API; none means one is made from <c>--server</c> or <c>SUWAGEVAL_URL</c> (the tests pass their own).</param>
    /// <param name="openBrowser">Opens a page for <c>diff</c>; none means the system's default browser (the tests record the page instead).</param>
    public static Command Create(Terminal terminal, HttpClient? http = null, Action<Uri>? openBrowser = null)
    {
        var archived = new Option<bool>("--archived") { Description = "Also list the archived versions (marked archived)." };
        var json = CommandOptions.Json("Print the versions as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();

        var command = new Command("versions", "List the agent's versions; archive or unarchive them; delete an archived one with its runs.")
        {
            archived,
            json,
            server,
            dir,
            Change("archive", "Archive versions: hidden from the web's default views, kept with their runs.", archive: true, http),
            Change("unarchive", "Restore archived versions.", archive: false, http),
            Show(http),
            Diff(http, openBrowser),
            Delete(terminal, http),
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

    private static Command Delete(Terminal terminal, HttpClient? http)
    {
        var id = new Argument<Guid>("versionId") { Description = "The archived version to delete, as `versions --archived` lists it." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Only print what would be deleted." };
        var yes = new Option<bool>("--yes") { Description = "Delete without asking (required when there is no terminal to ask on)." };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command(
            "delete",
            "Delete an archived version with everything that belongs to it: its runs, their samples and verdicts. Cannot be undone. Exits 1 when not confirmed."
        )
        {
            id,
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
                        var versionId = parsed.GetRequiredValue(id);
                        var output = parsed.InvocationConfiguration.Output;
                        var error = parsed.InvocationConfiguration.Error;
                        var contents = await client.VersionDeletionAsync(agent, versionId, cancellationToken);
                        await output.WriteLineAsync(VersionDeleteText.Preview(contents));
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
                            await output.WriteAsync("Type the version id to confirm: ");
                            if (!VersionDeleteText.Confirms(versionId, await terminal.Input.ReadLineAsync(cancellationToken)))
                            {
                                await error.WriteLineAsync("Not deleted: that is not the version id.");
                                return 1;
                            }
                        }
                        await client.DeleteVersionAsync(agent, versionId, cancellationToken);
                        await output.WriteLineAsync($"deleted version {versionId}");
                        return 0;
                    }
                )
        );
        return command;
    }

    private static Command Show(HttpClient? http)
    {
        var id = new Argument<Guid>("versionId") { Description = "The version, as `versions` lists it." };
        var json = CommandOptions.Json("Print the version with its contract as JSON.");
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("show", "Print one version of the agent: its id, label and creation time, then its contract as JSON.")
        {
            id,
            json,
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
                        var version = await client.VersionAsync(agent, parsed.GetRequiredValue(id), cancellationToken);
                        var output = parsed.InvocationConfiguration.Output;
                        if (parsed.GetValue(json))
                        {
                            await output.WriteLineAsync(OutputJson.Of(version));
                        }
                        else
                        {
                            await output.WriteLineAsync(
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"version {version.Id}{(version.Content.Label is null ? "" : $" ({version.Content.Label})")}  created {version.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z{(version.ArchivedAt is null ? "" : "  archived")}"
                                )
                            );
                            await output.WriteLineAsync(OutputJson.Of(version.Content));
                        }
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
        var json = CommandOptions.Json("Print the diff as JSON.");
        var open = new Option<bool>("--open") { Description = "Open the page showing the diff on the web instead of printing it." };
        var server = CommandOptions.Server();
        var dir = CommandOptions.Dir();
        var command = new Command("diff", "Print what changed between two versions of the agent (or --open it on the web).")
        {
            first,
            second,
            json,
            open,
            server,
            dir,
        };
        command.Validators.Add(result =>
        {
            if (result.GetValue(json) && result.GetValue(open))
            {
                result.AddError("--json and --open cannot be used together.");
            }
        });
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
                        if (!parsed.GetValue(open))
                        {
                            var diff = await client.VersionDiffAsync(agent, a, b, cancellationToken);
                            await parsed.InvocationConfiguration.Output.WriteLineAsync(
                                parsed.GetValue(json) ? OutputJson.Of(diff) : VersionDiffText.Describe(diff)
                            );
                            return 0;
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
