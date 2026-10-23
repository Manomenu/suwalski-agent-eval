using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Runs.Groups;

/// <summary>The routes of run groups: list them, archive or restore them, delete an archived one with its runs.</summary>
internal static class RunGroupEndpoints
{
    public static IEndpointRouteBuilder MapRunGroups(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/agents/{agent}/run-groups", ListAsync).Produces<IReadOnlyList<RunGroupSummary>>();
        app.MapPost("/agents/{agent}/run-groups/archive", ArchiveAsync).Produces(StatusCodes.Status204NoContent);
        app.MapGet("/agents/{agent}/run-groups/{name}/deletion", PreviewDeletionAsync).Produces<RunGroupDeletion>();
        app.MapDelete("/agents/{agent}/run-groups/{name}", DeleteAsync).Produces(StatusCodes.Status204NoContent);
        return app;
    }

    /// <summary>The groups of the agent's runs (run --group), archived ones too, the one with the newest run first. 404: no such agent.</summary>
    private static async Task<IResult> ListAsync(string agent, AgentStore agents, RunGroupStore groups, CancellationToken cancellationToken)
    {
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        return Results.Ok(
            (await groups.ListAsync(found.Id, cancellationToken))
                .Select(group => new RunGroupSummary
                {
                    Name = group.Name,
                    Runs = group.Runs,
                    Versions = group.Versions,
                    NewestRunAt = group.NewestRunAt,
                    ArchivedAt = group.ArchivedAt,
                })
                .ToList()
        );
    }

    /// <summary>Archives or restores groups; one unknown name changes nothing. 404: no such agent or group.</summary>
    private static async Task<IResult> ArchiveAsync(
        string agent,
        ArchiveRunGroupsRequest request,
        AgentStore agents,
        RunGroupStore groups,
        CancellationToken cancellationToken
    )
    {
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var unknown = await groups.UnknownAsync(found.Id, request.Names, cancellationToken);
        if (unknown.Count > 0)
        {
            return ProblemResults.NotFound("Run group", string.Join(", ", unknown));
        }
        await groups.ArchiveAsync(found.Id, request.Names, request.Archived, cancellationToken);
        return Results.NoContent();
    }

    private static Task<IResult> PreviewDeletionAsync(
        string agent,
        string name,
        AgentStore agents,
        RunGroupStore groups,
        CancellationToken cancellationToken
    ) => RunDeletionAsync(agent, name, dryRun: true, agents, groups, cancellationToken);

    private static Task<IResult> DeleteAsync(
        string agent,
        string name,
        AgentStore agents,
        RunGroupStore groups,
        CancellationToken cancellationToken
    ) => RunDeletionAsync(agent, name, dryRun: false, agents, groups, cancellationToken);

    // The preview answers exactly what a deletion would: the same refusals, then what would go.
    private static async Task<IResult> RunDeletionAsync(
        string agent,
        string name,
        bool dryRun,
        AgentStore agents,
        RunGroupStore groups,
        CancellationToken cancellationToken
    )
    {
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var result = await groups.DeleteAsync(found.Id, name, dryRun, cancellationToken);
        return result.ToHttp(
            "Run group",
            name,
            new DeletionRefusal("Run group not archived", "Only an archived run group can be deleted: archive it first."),
            new DeletionRefusal(
                "Run group has active runs",
                "A run of this group (or a re-check of one) is queued or running: cancel it or wait until it ends."
            )
        );
    }
}
