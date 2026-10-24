using System.Text.Json;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
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

    /// <summary>The conversation as a scenario file, taken as it is (<see cref="ScenarioDraftWriter"/>). 404: no such conversation.</summary>
    private static async Task<IResult> DraftAsync(Guid id, ConversationStore conversations, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(id, cancellationToken);
        if (conversation is null)
        {
            return ProblemResults.NotFound("Conversation", id.ToString());
        }
        var turns = await conversations.TurnsAsync(id, cancellationToken);
        return Results.Ok(
            ScenarioDraftWriter.Draft(
                conversation.ConversationKey,
                id,
                [
                    .. turns.Select(turn =>
                        (IReadOnlyList<ConversationItem>)(
                            JsonSerializer.Deserialize<ConversationItem[]>(turn.Items, ContractContent.Lenient) ?? []
                        )
                    ),
                ]
            )
        );
    }
}
