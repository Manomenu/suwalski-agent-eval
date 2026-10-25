using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Monitoring.Checking;
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
    /// The conversation as a scenario file (<see cref="ScenarioDraftWriter"/>): to its end, or up to its <c>upToUserMessage</c>-th user
    /// message or <c>upToAgentMessage</c>-th agent message (from 1, included), with the monitoring checks it failed as comments. 404: no
    /// such conversation; 422: both cuts, or a message the conversation does not have.
    /// </summary>
    private static async Task<IResult> DraftAsync(
        Guid id,
        [FromQuery] int? upToUserMessage,
        [FromQuery] int? upToAgentMessage,
        ConversationStore conversations,
        CheckingStore checking,
        CancellationToken cancellationToken
    )
    {
        var conversation = await conversations.GetAsync(id, cancellationToken);
        if (conversation is null)
        {
            return ProblemResults.NotFound("Conversation", id.ToString());
        }
        if (upToUserMessage is not null && upToAgentMessage is not null)
        {
            return ProblemResults.Invalid(
                "upToAgentMessage",
                "Name one message to copy up to: a user message or an agent message, not both."
            );
        }
        List<ConversationItem> items =
        [
            .. (await conversations.TurnsAsync(id, cancellationToken)).SelectMany(turn =>
                JsonSerializer.Deserialize<ConversationItem[]>(turn.Items, ContractContent.Lenient) ?? []
            ),
        ];
        var cut =
            upToUserMessage is { } user ? new MessageCut(true, user)
            : upToAgentMessage is { } agent ? new MessageCut(false, agent)
            : null;
        if (cut is not null && cut.IndexIn(items) is null)
        {
            return ProblemResults.Invalid(
                cut.UserMessage ? "upToUserMessage" : "upToAgentMessage",
                $"The conversation has no {cut.Describe()}."
            );
        }
        var failed = (await checking.FailedChecksAsync(id, cancellationToken))
            .Select(content =>
                JsonSerializer.Deserialize<CheckInput>(content, ContractContent.Lenient)
                ?? throw new InvalidOperationException("A stored check is empty.")
            )
            .ToList();
        return Results.Ok(ScenarioDraftWriter.Draft(conversation.ConversationKey, id, items, cut, failed));
    }
}
