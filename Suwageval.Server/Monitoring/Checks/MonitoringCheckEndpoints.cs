using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Monitoring;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Monitoring.Checks;

internal static class MonitoringCheckEndpoints
{
    public static IEndpointRouteBuilder MapMonitoringChecks(this IEndpointRouteBuilder app)
    {
        app.MapPut("/agents/{agent}/checks", PutAsync).Produces<PutChecksResponse>();
        app.MapGet("/agents/{agent}/checks", ListAsync).Produces<IReadOnlyList<MonitoringCheck>>();
        return app;
    }

    private static async Task<IResult> PutAsync(
        string agent,
        PutChecksRequest request,
        AgentStore agents,
        MonitoringCheckStore checks,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        if (row.Features.Monitoring is null)
        {
            return ProblemResults.FeatureOff(row.Name, "monitoring");
        }
        var problems = MonitoringCheckRules.Problems(request.Checks);
        if (problems.Count > 0)
        {
            return ProblemResults.Invalid(problems.Select(problem => new FieldError(problem.Path, problem.Message)));
        }
        return Results.Ok(new PutChecksResponse(await checks.ReplaceAsync(row.Id, request.Checks, cancellationToken)));
    }

    private static async Task<IResult> ListAsync(
        string agent,
        [FromQuery] string? environment,
        AgentStore agents,
        MonitoringCheckStore checks,
        TimeProvider time,
        CancellationToken cancellationToken
    )
    {
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        return Results.Ok(
            (await checks.ListAsync(row.Id, time.GetUtcNow(), environment, cancellationToken)).Select(check => new MonitoringCheck
            {
                Revision = check.Revision,
                CreatedAt = check.CreatedAt,
                Check =
                    JsonSerializer.Deserialize<CheckInput>(check.Content, ContractContent.Lenient)
                    ?? throw new InvalidOperationException("A stored check is empty."),
                Last7Days = new CheckRate(check.Passed7, check.Checked7),
                Last30Days = new CheckRate(check.Passed30, check.Checked30),
            })
        );
    }
}
