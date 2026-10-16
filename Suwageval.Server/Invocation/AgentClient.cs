using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.Contracts;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Invocation;

namespace Suwageval.Server.Invocation;

/// <summary>
/// Calls the agent under test (contract v0, TODO.md): <c>POST {endpointUrl}/suwageval/turn</c> with the conversation,
/// answered by the items the agent produced in that turn. Whatever goes wrong — the agent is down, slow, answers an error
/// or something unreadable — is returned as a failed <see cref="TurnResult"/>: it belongs to that sample's result and must
/// never stop a run.
/// </summary>
internal sealed class AgentClient(HttpClient http)
{
    private const int BodyExcerptLength = 300;

    /// <summary>The contract's JSON, without nulls on the way out: an agent that does not know a field must not be sent it.</summary>
    private static readonly JsonSerializerOptions Request = new(ContractJson.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<TurnResult> TurnAsync(
        Uri endpointUrl,
        IReadOnlyList<ConversationItem> conversation,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(endpointUrl);
        var url = new Uri(endpointUrl.AbsoluteUri.TrimEnd('/') + "/suwageval/turn");
        try
        {
            using var response = await http.PostAsJsonAsync(
                url,
                new TurnRequest { Conversation = conversation },
                Request,
                cancellationToken
            );
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var excerpt = body.Length > BodyExcerptLength ? body[..BodyExcerptLength] + "…" : body;
                return TurnResult.Failed(
                    $"The agent answered {(int)response.StatusCode} {response.ReasonPhrase}: {excerpt}".TrimEnd(' ', ':')
                );
            }
            var turn = await response.Content.ReadFromJsonAsync<TurnResponse>(ContractJson.Options, cancellationToken);
            return turn is null ? TurnResult.Failed("The agent's answer was empty.") : TurnResult.Succeeded(turn.Items);
        }
        catch (HttpRequestException error)
        {
            return TurnResult.Failed($"The agent could not be reached at {url}: {error.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TurnResult.Failed($"The agent did not answer within {http.Timeout.TotalSeconds:0.##} s.");
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            return TurnResult.Failed($"The agent's answer could not be read as the contract's items: {error.Message}");
        }
    }
}
