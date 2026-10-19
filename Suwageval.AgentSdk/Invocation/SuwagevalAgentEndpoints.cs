using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

public static class SuwagevalAgentEndpoints
{
    /// <summary>
    /// Maps <c>GET /suwageval/contract</c> and <c>POST /suwageval/turn</c> over the <see cref="IAgentContractSource"/> and the
    /// <see cref="ITurnHandler"/> registered in the services. Both read and write with <see cref="ContractJson.Options"/>, not
    /// with the application's own JSON settings.
    /// </summary>
    public static IEndpointRouteBuilder MapSuwagevalAgent(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(
            "/suwageval/contract",
            async (IAgentContractSource source, CancellationToken cancellationToken) =>
                Results.Json(await source.GetContractAsync(cancellationToken), ContractJson.Options)
        );
        endpoints.MapPost(
            "/suwageval/turn",
            async (HttpRequest request, ITurnHandler handler, CancellationToken cancellationToken) =>
            {
                var (turn, problem) = await ReadAsync(request, cancellationToken);
                return turn is null
                    ? Results.ValidationProblem(new Dictionary<string, string[]> { ["conversation"] = [problem] })
                    : Results.Json(await handler.HandleAsync(turn, cancellationToken), ContractJson.Options);
            }
        );
        return endpoints;
    }

    private static async Task<(TurnRequest? Turn, string Problem)> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        TurnRequest? turn;
        try
        {
            turn = await JsonSerializer.DeserializeAsync<TurnRequest>(request.Body, ContractJson.Options, cancellationToken);
        }
        catch (JsonException error)
        {
            return (null, $"The request is not a turn request: {error.Message}");
        }
        if (turn is null)
        {
            return (null, "The request body is empty.");
        }
        return turn.Conversation.Count == 0 ? (null, "The conversation is empty.")
            : turn.Conversation[^1] is not UserMessage ? (null, "The last item of the conversation must be a user message.")
            : (turn, "");
    }
}
