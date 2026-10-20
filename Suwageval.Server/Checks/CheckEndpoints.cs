using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Checks;

/// <summary>The routes of checks.</summary>
internal static class CheckEndpoints
{
    public static void MapChecks(this IEndpointRouteBuilder app)
    {
        app.MapPut("/agents/{agent}/checks", PutAsync);
        app.MapGet("/agents/{agent}/checks", ListAsync);
        app.MapDelete("/agents/{agent}/checks/{key}", DeleteAsync);
    }

    private static async Task<IResult> PutAsync(
        string agent,
        PutChecksRequest request,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var errors = CheckValidation.Validate(request.Checks);
        if (errors.Count == 0)
        {
            // Looked up first, so that a check naming a missing scenario writes nothing — and no other check of the request either.
            var live = (await scenarios.ListAsync(row.Id, cancellationToken: cancellationToken))
                .Select(scenario => scenario.Key)
                .ToHashSet();
            for (var i = 0; i < request.Checks.Count; i++)
            {
                if (!live.Contains(request.Checks[i].ScenarioKey))
                {
                    errors.Add(new FieldError($"checks[{i}].scenarioKey", $"There is no scenario '{request.Checks[i].ScenarioKey}'."));
                }
            }
        }
        if (errors.Count > 0)
        {
            return ProblemResults.Invalid(errors);
        }

        var items = request.Checks.Select(check => new CheckItem(
            check.Key,
            check.ScenarioKey,
            JsonSerializer.Serialize(check, ContractJson.Options)
        ));
        var results = await store.UpsertAsync(row.Id, [.. items], cancellationToken);
        var missing = results.Where(result => result.Outcome == CheckOutcome.UnknownScenario).ToList();
        if (missing.Count > 0)
        {
            // The scenario was deleted between the lookup and the write.
            return ProblemResults.Invalid(
                missing.Select(result => new FieldError("checks", $"The scenario of check '{result.Key}' is gone."))
            );
        }
        return Results.Ok(new PutChecksResponse([.. results.Select(result => new CheckResult(result.Key, ToOutcome(result.Outcome)))]));
    }

    private static async Task<IResult> ListAsync(
        string agent,
        string? scenario,
        AgentStore agents,
        CheckStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var checks = await store.ListAsync(row.Id, scenario, cancellationToken);
        return Results.Ok(
            checks.Select(check => new CheckDetail(
                check.Revision,
                check.CreatedAt,
                JsonSerializer.Deserialize<CheckInput>(check.Content, ContractJson.Options)
                    ?? throw new InvalidOperationException($"Check {check.Key} has no content.")
            ))
        );
    }

    private static async Task<IResult> DeleteAsync(
        string agent,
        string key,
        AgentStore agents,
        CheckStore store,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        return await store.DeleteAsync(row.Id, key, cancellationToken) ? Results.NoContent() : ProblemResults.NotFound("Check", key);
    }

    private static UpsertOutcome ToOutcome(CheckOutcome outcome) =>
        outcome switch
        {
            CheckOutcome.Created => UpsertOutcome.Created,
            CheckOutcome.Updated => UpsertOutcome.Updated,
            CheckOutcome.Unchanged => UpsertOutcome.Unchanged,
            CheckOutcome.UnknownScenario => throw new InvalidOperationException("An unknown scenario is reported before this."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
}
