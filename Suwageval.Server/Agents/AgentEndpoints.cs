using Suwageval.Contracts.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Agents;

/// <summary>The routes of agents and the history of their versions (read-only: a version is registered when a run starts, from the contract the agent serves).</summary>
internal static class AgentEndpoints
{
    public static void MapAgents(this IEndpointRouteBuilder app)
    {
        app.MapPost("/agents", CreateAsync);
        app.MapGet("/agents", ListAsync).Produces<IReadOnlyList<Agent>>();
        app.MapGet("/agents/{agent}", GetAsync).Produces<AgentDetail>();
        app.MapGet("/agents/{agent}/deletion", PreviewAgentDeletionAsync).Produces<AgentDeletion>();
        app.MapDelete("/agents/{agent}", DeleteAgentAsync).Produces(StatusCodes.Status204NoContent);
        app.MapGet("/agents/{agent}/versions", ListVersionsAsync).Produces<IReadOnlyList<AgentVersion>>();
        app.MapPost("/agents/{agent}/versions/archive", ArchiveVersionsAsync).Produces(StatusCodes.Status204NoContent);
        app.MapGet("/agents/{agent}/versions/{versionId:guid}", GetVersionAsync).Produces<AgentVersionDetail>();
        app.MapGet("/agents/{agent}/versions/{versionId:guid}/deletion", PreviewDeletionAsync).Produces<VersionDeletion>();
        app.MapDelete("/agents/{agent}/versions/{versionId:guid}", DeleteVersionAsync).Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> CreateAsync(AgentDefinition definition, AgentStore store, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add(new FieldError("name", "The name is empty."));
        }
        if (!Uri.TryCreate(definition.EndpointUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            errors.Add(new FieldError("endpointUrl", "The endpoint URL is an absolute http or https address."));
        }
        errors.AddRange(AgentRules.Problems(definition).Select(problem => new FieldError(problem.Path, problem.Message)));
        if (errors.Count > 0)
        {
            return ProblemResults.Invalid(errors);
        }

        var creation = await store.CreateAsync(definition.Name, definition.EndpointUrl, cancellationToken);
        var agent = creation.Agent;
        if (!creation.Created && agent.EndpointUrl != definition.EndpointUrl)
        {
            agent = await store.SetEndpointUrlAsync(agent.Id, definition.EndpointUrl, cancellationToken);
        }
        // The pushed definition is the source of truth: leaving samples: or regression: out puts the defaults back.
        if (agent.Samples != definition.Samples || !SameRegression(agent.Regression, definition.Regression))
        {
            agent = await store.SetSettingsAsync(agent.Id, definition.Samples, definition.Regression, cancellationToken);
        }
        return creation.Created
            ? Results.Created($"/agents/{Uri.EscapeDataString(definition.Name)}", ToAgent(agent))
            : Results.Ok(ToAgent(agent));
    }

    private static bool SameRegression(RegressionSettings? stored, RegressionSettings? pushed) =>
        stored?.History == pushed?.History
        && (stored?.Branches is null) == (pushed?.Branches is null)
        && (stored?.Branches ?? []).SequenceEqual(pushed?.Branches ?? []);

    private static async Task<IResult> ListAsync(AgentStore store, CancellationToken cancellationToken) =>
        Results.Ok((await store.ListAsync(cancellationToken)).Select(ToAgent));

    private static async Task<IResult> GetAsync(string agent, AgentStore store, CancellationToken cancellationToken)
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var counts = await store.CountAsync(row.Id, cancellationToken);
        return Results.Ok(
            new AgentDetail
            {
                Id = row.Id,
                Name = row.Name,
                EndpointUrl = row.EndpointUrl,
                CreatedAt = row.CreatedAt,
                Versions = counts.Versions,
                Scenarios = counts.Scenarios,
                Checks = counts.Checks,
                Samples = row.Samples,
                Regression = row.Regression,
            }
        );
    }

    private static async Task<IResult> ListVersionsAsync(string agent, AgentStore store, CancellationToken cancellationToken)
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        return Results.Ok((await store.ListVersionsAsync(row.Id, cancellationToken)).Select(ToVersion));
    }

    private static async Task<IResult> ArchiveVersionsAsync(
        string agent,
        ArchiveVersionsRequest request,
        AgentStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        // Looked up first, so that one unknown id changes nothing at all.
        var unknown = await store.UnknownVersionsAsync(row.Id, request.VersionIds, cancellationToken);
        if (unknown.Count > 0)
        {
            return ProblemResults.NotFound("Version", string.Join(", ", unknown));
        }
        await store.ArchiveVersionsAsync(row.Id, request.VersionIds, request.Archived, cancellationToken);
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
        var content = ContractContent.Read(version.Content);
        return Results.Ok(
            new AgentVersionDetail(
                version.Id,
                version.AgentId,
                version.ContentHash,
                version.CreatedAt,
                content with
                {
                    Label = version.Label,
                    Context = content.Context with
                    {
                        Retrieval = content.Context.Retrieval is { } retrieval
                            ? retrieval with
                            {
                                Description = version.DataDescription,
                            }
                            : null,
                    },
                },
                version.ArchivedAt
            )
        );
    }

    private static Task<IResult> PreviewAgentDeletionAsync(string agent, AgentStore store, CancellationToken cancellationToken) =>
        RunAgentDeletionAsync(agent, dryRun: true, store, cancellationToken);

    private static Task<IResult> DeleteAgentAsync(string agent, AgentStore store, CancellationToken cancellationToken) =>
        RunAgentDeletionAsync(agent, dryRun: false, store, cancellationToken);

    // The preview answers exactly what a deletion would: the same refusals, then what would go.
    private static async Task<IResult> RunAgentDeletionAsync(
        string agent,
        bool dryRun,
        AgentStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var result = await store.DeleteAgentAsync(row.Id, dryRun, cancellationToken);
        return result.Outcome switch
        {
            DeletionOutcome.NotFound => ProblemResults.NotFound("Agent", agent),
            DeletionOutcome.NotArchived => throw new InvalidOperationException("An agent is not archived."),
            DeletionOutcome.HasActiveRuns => ProblemResults.Conflict(
                "Agent has active runs",
                "A run of this agent is queued or running: cancel it or wait until it ends."
            ),
            DeletionOutcome.Allowed => Results.Ok(result.Contents),
            DeletionOutcome.Deleted => Results.NoContent(),
            _ => throw new InvalidOperationException($"Unexpected outcome {result.Outcome}."),
        };
    }

    private static Task<IResult> PreviewDeletionAsync(
        string agent,
        Guid versionId,
        AgentStore store,
        CancellationToken cancellationToken
    ) => DeleteAsync(agent, versionId, dryRun: true, store, cancellationToken);

    private static Task<IResult> DeleteVersionAsync(string agent, Guid versionId, AgentStore store, CancellationToken cancellationToken) =>
        DeleteAsync(agent, versionId, dryRun: false, store, cancellationToken);

    // The preview answers exactly what a deletion would: the same refusals, then what would go.
    private static async Task<IResult> DeleteAsync(
        string agent,
        Guid versionId,
        bool dryRun,
        AgentStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await store.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var result = await store.DeleteVersionAsync(row.Id, versionId, dryRun, cancellationToken);
        return result.Outcome switch
        {
            DeletionOutcome.NotFound => ProblemResults.NotFound("Version", versionId.ToString()),
            DeletionOutcome.NotArchived => ProblemResults.Conflict(
                "Version not archived",
                "Only an archived version can be deleted: archive it first."
            ),
            DeletionOutcome.HasActiveRuns => ProblemResults.Conflict(
                "Version has active runs",
                "A run of this version (or a re-check of one) is queued or running: cancel it or wait until it ends."
            ),
            DeletionOutcome.Allowed => Results.Ok(result.Contents),
            DeletionOutcome.Deleted => Results.NoContent(),
            _ => throw new InvalidOperationException($"Unexpected outcome {result.Outcome}."),
        };
    }

    private static Agent ToAgent(AgentRow row) =>
        new()
        {
            Id = row.Id,
            Name = row.Name,
            EndpointUrl = row.EndpointUrl,
            CreatedAt = row.CreatedAt,
        };

    private static AgentVersion ToVersion(AgentVersionRow row)
    {
        var contract = ContractContent.Read(row.Content);
        return new AgentVersion(
            row.Id,
            row.AgentId,
            row.ContentHash,
            row.CreatedAt,
            ContractContent.ModelNames(contract),
            row.Label,
            row.ArchivedAt,
            contract.Context.Retrieval?.DataVersion,
            row.DataDescription
        );
    }
}
