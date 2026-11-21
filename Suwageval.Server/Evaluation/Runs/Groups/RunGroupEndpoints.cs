using Suwageval.Contracts.Refusals;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Evaluation.Runs.Groups;

internal static class RunGroupEndpoints
{
    public static IEndpointRouteBuilder MapRunGroups(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/agents/{agent}/run-groups", ListAsync)
            .Produces<IReadOnlyList<RunGroupSummary>>()
            .ProducesRefusal(StatusCodes.Status404NotFound);
        app.MapPost("/agents/{agent}/run-groups/archive", ArchiveAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesRefusal(StatusCodes.Status404NotFound);
        app.MapPut("/agents/{agent}/run-groups/{name}/note", SetNoteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesRefusal(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);
        app.MapGet("/agents/{agent}/run-groups/{name}/deletion", PreviewDeletionAsync)
            .Produces<RunGroupDeletion>()
            .ProducesRefusal(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
        app.MapDelete("/agents/{agent}/run-groups/{name}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesRefusal(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
        return app;
    }

    private static async Task<IResult> ListAsync(string agent, AgentStore agents, RunGroupStore groups, CancellationToken cancellationToken)
    {
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.AgentNotFound(agent);
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
            return ProblemResults.AgentNotFound(agent);
        }
        var unknown = await groups.UnknownAsync(found.Id, request.Names, cancellationToken);
        if (unknown.Count > 0)
        {
            return ProblemResults.RunGroupNotFound(string.Join(", ", unknown));
        }
        await groups.ArchiveAsync(found.Id, request.Names, request.Archived, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SetNoteAsync(
        string agent,
        string name,
        NoteRequest request,
        AgentStore agents,
        RunGroupStore groups,
        CancellationToken cancellationToken
    )
    {
        if (RunGroupRules.Problem(name) is { } groupProblem)
        {
            return ProblemResults.Invalid("name", groupProblem, RunGroupRules.Fix);
        }
        if (NoteRules.Problem(request.Note) is { } problem)
        {
            return ProblemResults.Invalid("note", problem, NoteRules.Fix);
        }
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.AgentNotFound(agent);
        }
        return await groups.SetNoteAsync(found.Id, name, NoteRules.Normalize(request.Note), cancellationToken)
            ? Results.NoContent()
            : ProblemResults.RunGroupNotFound(name);
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
            return ProblemResults.AgentNotFound(agent);
        }
        var result = await groups.DeleteAsync(found.Id, name, dryRun, cancellationToken);
        return result.ToHttp(
            ProblemResults.RunGroupNotFound(name),
            new DeletionRefusal(
                RefusalCode.RunGroupNotArchived,
                "Run group not archived",
                "Only an archived run group can be deleted: archive it first."
            ),
            new DeletionRefusal(
                RefusalCode.RunsActive,
                "Run group has active runs",
                "A run of this group (or a re-check of one) is queued or running: cancel it or wait until it ends."
            )
        );
    }
}
