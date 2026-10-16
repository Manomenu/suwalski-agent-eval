using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Scenarios;

/// <summary>The routes of scenarios, their revisions and tags.</summary>
internal static class ScenarioEndpoints
{
    public static void MapScenarios(this IEndpointRouteBuilder app)
    {
        app.MapPut("/agents/{agent}/scenarios", PutAsync);
        app.MapGet("/agents/{agent}/scenarios", ListAsync).Produces<IReadOnlyList<ScenarioSummary>>();
        app.MapGet("/agents/{agent}/scenarios/{key}", GetAsync);
        app.MapGet("/agents/{agent}/scenarios/{key}/revisions", ListRevisionsAsync);
        app.MapDelete("/agents/{agent}/scenarios/{key}", DeleteAsync);
        app.MapPost("/agents/{agent}/scenarios/tags", ChangeTagsAsync);
        app.MapGet("/agents/{agent}/tags", ListTagsAsync);
    }

    private static async Task<IResult> PutAsync(
        string agent,
        PutScenariosRequest request,
        AgentStore agents,
        ScenarioStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var errors = ScenarioValidation.Validate(request.Scenarios);
        if (errors.Count > 0)
        {
            return ProblemResults.Invalid(errors);
        }

        // Tags are metadata: they stay out of the content, so changing them makes no new revision.
        var items = request
            .Scenarios.Select(scenario => new ScenarioItem(
                scenario.Key,
                scenario.Tags,
                JsonSerializer.Serialize(scenario with { Tags = [] }, ContractJson.Options)
            ))
            .ToList();
        var results = await store.UpsertAsync(row.Id, items, cancellationToken);
        return Results.Ok(
            new PutScenariosResponse([.. results.Select(result => new ScenarioResult(result.Key, ToOutcome(result.Outcome)))])
        );
    }

    private static async Task<IResult> ListAsync(
        string agent,
        string? tag,
        AgentStore agents,
        ScenarioStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var scenarios = await store.ListAsync(row.Id, tag, cancellationToken);
        return Results.Ok(
            scenarios.Select(scenario => new ScenarioSummary(scenario.Key, scenario.Tags, scenario.Revision, scenario.CreatedAt))
        );
    }

    private static async Task<IResult> GetAsync(
        string agent,
        string key,
        AgentStore agents,
        ScenarioStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var scenario = await store.GetAsync(row.Id, key, cancellationToken);
        if (scenario is null)
        {
            return ProblemResults.NotFound("Scenario", key);
        }
        return Results.Ok(new ScenarioDetail(scenario.Revision, scenario.CreatedAt, Read(scenario.Content) with { Tags = scenario.Tags }));
    }

    private static async Task<IResult> ListRevisionsAsync(
        string agent,
        string key,
        AgentStore agents,
        ScenarioStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var revisions = await store.ListRevisionsAsync(row.Id, key, cancellationToken);
        if (revisions.Count == 0)
        {
            return ProblemResults.NotFound("Scenario", key);
        }
        return Results.Ok(
            revisions.Select(revision => new ScenarioRevision(
                revision.Revision,
                revision.ContentHash,
                revision.CreatedAt,
                Read(revision.Content)
            ))
        );
    }

    private static async Task<IResult> DeleteAsync(
        string agent,
        string key,
        AgentStore agents,
        ScenarioStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        return await store.DeleteAsync(row.Id, key, cancellationToken) ? Results.NoContent() : ProblemResults.NotFound("Scenario", key);
    }

    private static async Task<IResult> ChangeTagsAsync(
        string agent,
        TagChangeRequest request,
        AgentStore agents,
        ScenarioStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var errors = ScenarioValidation.Validate(request);
        if (errors.Count > 0)
        {
            return ProblemResults.Invalid(errors);
        }
        // Looked up first, so that one unknown key changes nothing at all.
        var live = (await store.ListAsync(row.Id, cancellationToken: cancellationToken)).Select(scenario => scenario.Key).ToHashSet();
        var unknown = request.ScenarioKeys.Distinct().Where(key => !live.Contains(key)).ToList();
        if (unknown.Count > 0)
        {
            return ProblemResults.Invalid(unknown.Select(key => new FieldError("scenarioKeys", $"There is no scenario '{key}'.")));
        }

        var change = await store.ChangeTagsAsync(row.Id, request.ScenarioKeys, request.Add, request.Remove, cancellationToken);
        return Results.Ok(new TagChangeResponse([.. change.Changed.Select(changed => new ScenarioTagSet(changed.Key, changed.Tags))]));
    }

    private static async Task<IResult> ListTagsAsync(
        string agent,
        AgentStore agents,
        ScenarioStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        return Results.Ok((await store.ListTagsAsync(row.Id, cancellationToken)).Select(tag => new TagUsage(tag.Tag, tag.ScenarioCount)));
    }

    private static ScenarioInput Read(string content) =>
        JsonSerializer.Deserialize<ScenarioInput>(content, ContractJson.Options)
        ?? throw new InvalidOperationException("A stored scenario has no content.");

    private static UpsertOutcome ToOutcome(ScenarioOutcome outcome) =>
        outcome switch
        {
            ScenarioOutcome.Created => UpsertOutcome.Created,
            ScenarioOutcome.Updated => UpsertOutcome.Updated,
            ScenarioOutcome.Unchanged => UpsertOutcome.Unchanged,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
}
