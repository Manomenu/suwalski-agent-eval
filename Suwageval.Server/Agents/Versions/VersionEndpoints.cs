using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Agents.Versions;

/// <summary>The routes of the history of an agent's versions (a version is registered when a run starts, from the contract the agent serves): list, archive, compare, read, delete.</summary>
internal static class VersionEndpoints
{
    public static IEndpointRouteBuilder MapVersions(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/agents/{agent}/versions", ListVersionsAsync).Produces<IReadOnlyList<AgentVersion>>();
        app.MapPost("/agents/{agent}/versions/archive", ArchiveVersionsAsync).Produces(StatusCodes.Status204NoContent);
        app.MapGet("/agents/{agent}/versions/diff", DiffVersionsAsync).Produces<VersionDiff>();
        app.MapGet("/agents/{agent}/versions/{versionId:guid}", GetVersionAsync).Produces<AgentVersionDetail>();
        app.MapGet("/agents/{agent}/versions/{versionId:guid}/deletion", PreviewDeletionAsync).Produces<VersionDeletion>();
        app.MapDelete("/agents/{agent}/versions/{versionId:guid}", DeleteVersionAsync).Produces(StatusCodes.Status204NoContent);
        return app;
    }

    /// <summary>
    /// The agent's versions, newest first. <c>grouping</c> (default <c>All</c>): <c>None</c> only the ordinary versions, <c>Any</c> only those whose
    /// every run is in a group (<c>groupRunsOnly</c>), as the runs list does for runs.
    /// </summary>
    private static async Task<IResult> ListVersionsAsync(
        string agent,
        [FromQuery] RunGrouping? grouping,
        AgentStore store,
        VersionStore versions,
        CancellationToken cancellationToken
    )
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var counts = await versions.RunCountsAsync(row.Id, cancellationToken);
        var data = await versions.DataAsync(row.Id, cancellationToken);
        return Results.Ok(
            (await store.ListVersionsAsync(row.Id, cancellationToken))
                .Where(version =>
                    (grouping ?? RunGrouping.All) switch
                    {
                        RunGrouping.None => !CountsOf(counts, version.Id).GroupRunsOnly,
                        RunGrouping.Any => CountsOf(counts, version.Id).GroupRunsOnly,
                        RunGrouping.All or _ => true,
                    }
                )
                .Select(version => ToVersion(version, CountsOf(counts, version.Id), data.GetValueOrDefault(version.Id, [])))
        );
    }

    private static async Task<IResult> ArchiveVersionsAsync(
        string agent,
        ArchiveVersionsRequest request,
        AgentStore store,
        VersionStore versions,
        CancellationToken cancellationToken
    )
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        // Looked up first, so that one unknown id changes nothing at all.
        var unknown = await versions.UnknownVersionsAsync(row.Id, request.VersionIds, cancellationToken);
        if (unknown.Count > 0)
        {
            return ProblemResults.NotFound("Version", string.Join(", ", unknown));
        }
        await versions.ArchiveVersionsAsync(row.Id, request.VersionIds, request.Archived, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetVersionAsync(string agent, Guid versionId, AgentStore store, CancellationToken cancellationToken)
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var version = await store.GetVersionAsync(versionId, cancellationToken);
        if (version is null || version.AgentId != row.Id)
        {
            return ProblemResults.NotFound("Version", versionId.ToString());
        }
        return Results.Ok(
            new AgentVersionDetail(
                version.Id,
                version.AgentId,
                version.ContentHash,
                version.CreatedAt,
                ContentOf(version),
                version.ArchivedAt
            )
        );
    }

    /// <summary>The contract of a version with its label, which is kept beside the content.</summary>
    private static AgentContract ContentOf(AgentVersionRow version) => ContractContent.Read(version.Content) with { Label = version.Label };

    /// <summary>
    /// What changed between two versions of an agent (<c>from</c> and <c>to</c>, version ids): both contracts as YAML, compared by
    /// lines, the older version first. 404: no such agent or version; 422: a missing or equal id, or a version of another agent.
    /// </summary>
    private static async Task<IResult> DiffVersionsAsync(
        string agent,
        [FromQuery] Guid? from,
        [FromQuery] Guid? to,
        AgentStore store,
        VersionStore versions,
        CancellationToken cancellationToken
    )
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        if (from is null || to is null)
        {
            return ProblemResults.Invalid([
                .. new[] { (from, "from"), (to, "to") }
                    .Where(side => side.Item1 is null)
                    .Select(side => new FieldError(side.Item2, "Name the version to compare.")),
            ]);
        }
        if (from == to)
        {
            return ProblemResults.Invalid("to", "A version is not compared with itself.");
        }
        var first = await store.GetVersionAsync(from.Value, cancellationToken);
        var second = await store.GetVersionAsync(to.Value, cancellationToken);
        if (first is null)
        {
            return ProblemResults.NotFound("Version", from.Value.ToString());
        }
        if (second is null)
        {
            return ProblemResults.NotFound("Version", to.Value.ToString());
        }
        if (first.AgentId != row.Id || second.AgentId != row.Id)
        {
            return ProblemResults.Invalid(first.AgentId != row.Id ? "from" : "to", $"The version is not one of agent '{agent}'.");
        }
        // The older one is the base; versions made at the same time stay in the order asked for.
        var (older, newer) = second.CreatedAt < first.CreatedAt ? (second, first) : (first, second);
        var counts = await versions.RunCountsAsync(row.Id, cancellationToken);
        var data = await versions.DataAsync(row.Id, cancellationToken);
        var rows = LineDiff.Rows(ContractYaml.Of(ContentOf(older)), ContractYaml.Of(ContentOf(newer)));
        return Results.Ok(
            new VersionDiff
            {
                From = ToVersion(older, CountsOf(counts, older.Id), data.GetValueOrDefault(older.Id, [])),
                To = ToVersion(newer, CountsOf(counts, newer.Id), data.GetValueOrDefault(newer.Id, [])),
                Added = rows.Count(line => line.Kind == DiffKind.Added),
                Removed = rows.Count(line => line.Kind == DiffKind.Removed),
                Blocks = LineDiff.Blocks(rows),
            }
        );
    }

    private static Task<IResult> PreviewDeletionAsync(
        string agent,
        Guid versionId,
        AgentStore store,
        VersionStore versions,
        CancellationToken cancellationToken
    ) => DeleteAsync(agent, versionId, dryRun: true, store, versions, cancellationToken);

    private static Task<IResult> DeleteVersionAsync(
        string agent,
        Guid versionId,
        AgentStore store,
        VersionStore versions,
        CancellationToken cancellationToken
    ) => DeleteAsync(agent, versionId, dryRun: false, store, versions, cancellationToken);

    // The preview answers exactly what a deletion would: the same refusals, then what would go.
    private static async Task<IResult> DeleteAsync(
        string agent,
        Guid versionId,
        bool dryRun,
        AgentStore store,
        VersionStore versions,
        CancellationToken cancellationToken
    )
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var result = await versions.DeleteVersionAsync(row.Id, versionId, dryRun, cancellationToken);
        return result.ToHttp(
            "Version",
            versionId.ToString(),
            new DeletionRefusal("Version not archived", "Only an archived version can be deleted: archive it first."),
            new DeletionRefusal(
                "Version has active runs",
                "A run of this version (or a re-check of one) is queued or running: cancel it or wait until it ends."
            )
        );
    }

    private static VersionRunCounts CountsOf(IReadOnlyDictionary<Guid, VersionRunCounts> counts, Guid versionId) =>
        counts.GetValueOrDefault(versionId, VersionRunCounts.None);

    private static AgentVersion ToVersion(AgentVersionRow row, VersionRunCounts counts, IReadOnlyList<RetrievalData> data)
    {
        var contract = ContractContent.Read(row.Content);
        return new AgentVersion(
            row.Id,
            row.AgentId,
            row.ContentHash,
            row.CreatedAt,
            ContractContent.ModelNames(contract),
            row.Label,
            row.ArchivedAt
        )
        {
            Data = data,
            GroupRunsOnly = counts.GroupRunsOnly,
            Runs = counts.Runs,
            GroupRuns = counts.GroupRuns,
        };
    }
}
