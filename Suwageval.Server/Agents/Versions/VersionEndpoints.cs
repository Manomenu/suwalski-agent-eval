using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Agents.Versions;

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

    private static async Task<IResult> ListVersionsAsync(
        string agent,
        AgentStore store,
        VersionStore versions,
        TimeProvider time,
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
        var monitoring = await versions.MonitoringAsync(row.Id, time.GetUtcNow().AddDays(-30), cancellationToken);
        return Results.Ok(
            (await store.ListVersionsAsync(row.Id, cancellationToken)).Select(version =>
                ToVersion(version, CountsOf(counts, version.Id), data.GetValueOrDefault(version.Id, [])) with
                {
                    Monitoring = monitoring.GetValueOrDefault(version.Id, []),
                }
            )
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
        // Why: looked up first so that one unknown id changes nothing at all
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

    private static AgentContract ContentOf(AgentVersionRow version) => ContractContent.Read(version.Content) with { Label = version.Label };

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
        // Why: the older version is the base; versions made at the same time keep the order asked for
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
            Runs = counts.Runs,
            GroupRuns = counts.GroupRuns,
        };
    }
}
