using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Monitoring.Checking;
using Suwageval.Server.Monitoring.Conversations;
using Suwageval.Server.Problems;

namespace Suwageval.Server.ScenarioDrafts;

internal static class ScenarioDraftEndpoints
{
    public static IEndpointRouteBuilder MapScenarioDrafts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/conversations/{id:guid}/scenario", DraftAsync)
            .Produces<ScenarioDraft>()
            .ProducesRefusal(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);
        return app;
    }

    private static async Task<IResult> DraftAsync(
        Guid id,
        [FromQuery] int? upToUserMessage,
        [FromQuery] int? upToAgentMessage,
        [FromQuery] bool? simulate,
        [FromQuery] int? turns,
        ConversationStore conversations,
        CheckingStore checking,
        UserDescriber describer,
        CancellationToken cancellationToken
    )
    {
        var conversation = await conversations.GetAsync(id, cancellationToken);
        if (conversation is null)
        {
            return ProblemResults.ConversationNotFound(id);
        }
        if (upToUserMessage is not null && upToAgentMessage is not null)
        {
            return ProblemResults.Invalid(
                "upToAgentMessage",
                "Both a user message and an agent message are named to copy up to.",
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
        var end = cut?.IndexIn(items);
        if (cut is not null && end is null)
        {
            return ProblemResults.Invalid(
                cut.UserMessage ? "upToUserMessage" : "upToAgentMessage",
                $"The conversation has no {cut.Describe()}.",
                "Name a message the conversation has."
            );
        }
        if (simulate is not true && turns is not null)
        {
            return ProblemResults.Invalid(
                "turns",
                "Turns are given without simulate.",
                "Set simulate too: turns are the simulated rest's."
            );
        }
        if (turns is < 1 or > ContinuationRules.MaxTurns)
        {
            return ProblemResults.Invalid(
                "turns",
                $"Turns are not from 1 to {ContinuationRules.MaxTurns}.",
                $"Ask for 1 to {ContinuationRules.MaxTurns} turns."
            );
        }
        (UserDescription User, int Turns)? simulation = null;
        if (simulate is true)
        {
            if (cut is null || end is not { } at)
            {
                return ProblemResults.Invalid(
                    "simulate",
                    "Simulate is set without a message to copy up to.",
                    "Name the message to copy up to: the rest after it is simulated."
                );
            }
            if (DraftTurns.After(items, at) == 0)
            {
                return ProblemResults.Invalid(
                    "simulate",
                    $"Nothing came after {cut.Describe()} to simulate.",
                    "Copy up to an earlier message, or leave simulate out."
                );
            }
            simulation = (await describer.DescribeAsync(items, cancellationToken), turns ?? DraftTurns.After(items, at));
        }
        var failed = (await checking.FailedChecksAsync(id, cancellationToken))
            .Select(content =>
                JsonSerializer.Deserialize<CheckInput>(content, ContractContent.Lenient)
                ?? throw new InvalidOperationException("A stored check is empty.")
            )
            .ToList();
        return Results.Ok(ScenarioDraftWriter.Draft(conversation.ConversationKey, id, items, cut, failed, simulation));
    }
}
