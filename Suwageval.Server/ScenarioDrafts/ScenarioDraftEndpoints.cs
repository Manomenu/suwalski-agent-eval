using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Monitoring.Conversations;
using Suwageval.Server.Problems;

namespace Suwageval.Server.ScenarioDrafts;

/// <summary>
/// The one way from Monitoring to Evaluation: a monitored conversation drafted as a scenario file. It composes both areas, so it stands
/// above them, like <c>Api</c>.
/// </summary>
internal static class ScenarioDraftEndpoints
{
    public static IEndpointRouteBuilder MapScenarioDrafts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/conversations/{id:guid}/scenario", DraftAsync).Produces<ScenarioDraft>();
        return app;
    }

    /// <summary>
    /// The conversation as a scenario file (<see cref="ScenarioDraftWriter"/>): taken as it is, or with <c>untilTurn</c> (from 1) replayed to
    /// that turn and simulated after it, the simulated user drafted by the simulator's model. 404: no such conversation; 422: no such turn.
    /// </summary>
    private static async Task<IResult> DraftAsync(
        Guid id,
        [FromQuery] int? untilTurn,
        ConversationStore conversations,
        UserDescriber describer,
        CancellationToken cancellationToken
    )
    {
        var conversation = await conversations.GetAsync(id, cancellationToken);
        if (conversation is null)
        {
            return ProblemResults.NotFound("Conversation", id.ToString());
        }
        IReadOnlyList<IReadOnlyList<ConversationItem>> turns =
        [
            .. (await conversations.TurnsAsync(id, cancellationToken)).Select(turn =>
                (IReadOnlyList<ConversationItem>)(JsonSerializer.Deserialize<ConversationItem[]>(turn.Items, ContractContent.Lenient) ?? [])
            ),
        ];
        if (untilTurn is { } until && (until < 1 || until > turns.Count))
        {
            return ProblemResults.Invalid("untilTurn", $"untilTurn is a turn of the conversation: from 1 to {turns.Count}.");
        }
        var description =
            untilTurn < turns.Count ? await describer.DescribeAsync([.. turns.SelectMany(turn => turn)], cancellationToken) : null;
        return Results.Ok(ScenarioDraftWriter.Draft(conversation.ConversationKey, id, turns, untilTurn, description));
    }
}
