using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Agents;

/// <summary>The routes of agents.</summary>
internal static class AgentEndpoints
{
    public static void MapAgents(this IEndpointRouteBuilder app)
    {
        app.MapPost("/agents", CreateAsync);
        app.MapGet("/agents", ListAsync).Produces<IReadOnlyList<Agent>>();
        app.MapGet("/agents/{agent}", GetAsync).Produces<AgentDetail>();
        app.MapGet("/agents/{agent}/deletion", PreviewAgentDeletionAsync).Produces<AgentDeletion>();
        app.MapDelete("/agents/{agent}", DeleteAgentAsync).Produces(StatusCodes.Status204NoContent);
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
        // The pushed definition is the source of truth: leaving samples:, regression: or features: out puts the defaults back.
        var features = definition.Features ?? new AgentFeatures();
        if (
            agent.Samples != definition.Samples
            || !SameRegression(agent.Regression, definition.Regression)
            || !SameFeatures(agent.Features, features)
        )
        {
            agent = await store.SetSettingsAsync(agent.Id, definition.Samples, definition.Regression, features, cancellationToken);
        }
        return creation.Created
            ? Results.Created($"/agents/{Uri.EscapeDataString(definition.Name)}", ToAgent(agent))
            : Results.Ok(ToAgent(agent));
    }

    private static bool SameRegression(RegressionSettings? stored, RegressionSettings? pushed) =>
        stored?.History == pushed?.History
        && (stored?.Branches is null) == (pushed?.Branches is null)
        && (stored?.Branches ?? []).SequenceEqual(pushed?.Branches ?? []);

    private static bool SameFeatures(AgentFeatures stored, AgentFeatures pushed) =>
        JsonSerializer.Serialize(stored, ContractJson.Options) == JsonSerializer.Serialize(pushed, ContractJson.Options);

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
                Features = row.Features,
            }
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
        return result.ToHttp(
            "Agent",
            agent,
            null,
            new DeletionRefusal("Agent has active runs", "A run of this agent is queued or running: cancel it or wait until it ends.")
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
}
