using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Invocation;

namespace Suwageval.Server.Invocation;

/// <summary>
/// Calls the agent under test (contract v0, docs/scenario-format.md): <c>GET {endpointUrl}/suwageval/contract</c>, answered
/// by what the agent is (its version), and <c>POST {endpointUrl}/suwageval/turn</c> with the conversation, answered by the
/// items the agent produced in that turn. Whatever goes wrong — the agent is down, slow, answers an error
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

    /// <summary>The agent's contract: its prompt, model, tools and context as it runs now. Never throws for what the agent does.</summary>
    public async Task<ContractResult> GetContractAsync(Uri endpointUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpointUrl);
        var url = new Uri(endpointUrl.AbsoluteUri.TrimEnd('/') + "/suwageval/contract");
        var problem = $"The agent's contract could not be read from {url}: ";
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var excerpt = body.Length > BodyExcerptLength ? body[..BodyExcerptLength] + "…" : body;
                return ContractResult.Failed(
                    $"{problem}the agent answered {(int)response.StatusCode} {response.ReasonPhrase}: {excerpt}".TrimEnd(' ', ':')
                );
            }
            var contract = await response.Content.ReadFromJsonAsync<AgentContract>(ContractJson.Options, cancellationToken);
            return contract is null ? ContractResult.Failed($"{problem}the answer was empty.") : ContractResult.Succeeded(contract);
        }
        catch (HttpRequestException error)
        {
            return ContractResult.Failed($"{problem}{error.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ContractResult.Failed($"{problem}no answer within {http.Timeout.TotalSeconds:0.##} s.");
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            return ContractResult.Failed($"{problem}it does not match the contract: {error.Message}");
        }
    }

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
