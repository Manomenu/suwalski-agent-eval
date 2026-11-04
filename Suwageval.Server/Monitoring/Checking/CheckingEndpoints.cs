using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Monitoring;
using Suwageval.Server.Agents;
using Suwageval.Server.Monitoring.Conversations;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Monitoring.Checking;

internal static class CheckingEndpoints
{
    public const int DefaultDays = 30;

    public const int MaxDays = 365;

    public static IEndpointRouteBuilder MapChecking(this IEndpointRouteBuilder app)
    {
        app.MapGet("/agents/{agent}/trend", TrendAsync).Produces<MonitoringTrend>();
        return app;
    }

    private static async Task<IResult> TrendAsync(
        string agent,
        [FromQuery] int? days,
        [FromQuery] string? environment,
        AgentStore agents,
        MonitoringTrendStore trend,
        ConversationStore conversations,
        TimeProvider time,
        CancellationToken cancellationToken
    )
    {
        var span = days ?? DefaultDays;
        if (span is < 1 or > MaxDays)
        {
            return ProblemResults.Invalid("days", $"days is from 1 to {MaxDays}.");
        }
        var row = await agents.GetAsync(agent, cancellationToken);
        if (row is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var since = new DateTimeOffset(time.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero).AddDays(1 - span);
        var found = await trend.TrendAsync(row.Id, since, string.IsNullOrEmpty(environment) ? null : environment, cancellationToken);
        return Results.Ok(found with { Environments = await conversations.EnvironmentsAsync(row.Id, cancellationToken) });
    }
}
