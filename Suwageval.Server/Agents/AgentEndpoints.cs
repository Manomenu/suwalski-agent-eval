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
        app.MapGet("/agents/{agent}/versions", ListVersionsAsync).Produces<IReadOnlyList<AgentVersion>>();
        app.MapPost("/agents/{agent}/versions/archive", ArchiveVersionsAsync).Produces(StatusCodes.Status204NoContent);
        app.MapGet("/agents/{agent}/versions/{versionId:guid}", GetVersionAsync).Produces<AgentVersionDetail>();
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
        if (errors.Count > 0)
        {
            return ProblemResults.Invalid(errors);
        }

        var creation = await store.CreateAsync(definition.Name, definition.EndpointUrl, cancellationToken);
        if (creation.Created)
        {
            return Results.Created($"/agents/{Uri.EscapeDataString(definition.Name)}", ToAgent(creation.Agent));
        }
        var agent =
            creation.Agent.EndpointUrl == definition.EndpointUrl
                ? creation.Agent
                : await store.SetEndpointUrlAsync(creation.Agent.Id, definition.EndpointUrl, cancellationToken);
        return Results.Ok(ToAgent(agent));
    }

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
                version.ArchivedAt,
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
                }
            )
        );
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
            row.Label,
            row.ContentHash,
            row.CreatedAt,
            row.ArchivedAt,
            ContractContent.ModelNames(contract),
            contract.Context.Retrieval?.DataVersion,
            row.DataDescription
        );
    }
}
