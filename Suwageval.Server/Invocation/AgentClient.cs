using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;

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
    public Task<ContractResult> GetContractAsync(Uri endpointUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpointUrl);
        var url = new Uri(endpointUrl.AbsoluteUri.TrimEnd('/') + "/suwageval/contract");
        var problem = $"The agent's contract could not be read from {url}: ";
        return SendAsync<AgentContract, ContractResult>(
            new HttpRequestMessage(HttpMethod.Get, url),
            new Messages(
                Answered: $"{problem}the agent answered",
                Empty: $"{problem}the answer was empty.",
                Unreachable: problem,
                NoAnswer: $"{problem}no answer within",
                Unreadable: $"{problem}it does not match the contract: "
            ),
            ContractResult.Succeeded,
            ContractResult.Failed,
            cancellationToken
        );
    }

    /// <summary>
    /// One turn: the conversation and the scenario's fixtures in, the items the agent produced out. The result carries the
    /// wall-clock time of the call, whether it succeeded or not.
    /// </summary>
    public async Task<TurnResult> TurnAsync(
        Uri endpointUrl,
        IReadOnlyList<ConversationItem> conversation,
        TurnContext context,
        CancellationToken cancellationToken = default
    )
    {
        var clock = Stopwatch.StartNew();
        var result = await CallAsync(endpointUrl, conversation, context, cancellationToken);
        return result with { DurationMs = (int)clock.ElapsedMilliseconds };
    }

    /// <summary>The <c>baggage</c> header's value: the keys of <see cref="SuwagevalBaggage"/>, a list or group left out when empty.</summary>
    internal static string Baggage(TurnContext context)
    {
        var members = new List<KeyValuePair<string, string>>
        {
            new(SuwagevalBaggage.RunId, context.RunId.ToString()),
            new(SuwagevalBaggage.ScenarioKey, context.ScenarioKey),
            new(SuwagevalBaggage.ScenarioTypeKey, context.ScenarioType.ToString()),
            new(SuwagevalBaggage.ScenarioSample, context.Sample.ToString(CultureInfo.InvariantCulture)),
        };
        if (context.ScenarioTags.Count > 0)
        {
            members.Add(new(SuwagevalBaggage.ScenarioTags, string.Join(',', context.ScenarioTags)));
        }
        if (context.RunTags.Count > 0)
        {
            members.Add(new(SuwagevalBaggage.RunTags, string.Join(',', context.RunTags)));
        }
        if (context.RunGroup is { Length: > 0 } group)
        {
            members.Add(new(SuwagevalBaggage.RunGroup, group));
        }
        return SuwagevalBaggage.Encode(members);
    }

    private Task<TurnResult> CallAsync(
        Uri endpointUrl,
        IReadOnlyList<ConversationItem> conversation,
        TurnContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(endpointUrl);
        ArgumentNullException.ThrowIfNull(context);
        var url = new Uri(endpointUrl.AbsoluteUri.TrimEnd('/') + "/suwageval/turn");
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(
                new TurnRequest
                {
                    Conversation = conversation,
                    Fixtures = context.Fixtures,
                    ConversationId = context.ConversationId,
                },
                options: Request
            ),
        };
        request.Headers.Add(SuwagevalBaggage.HeaderName, Baggage(context));
        return SendAsync<TurnResponse, TurnResult>(
            request,
            new Messages(
                Answered: "The agent answered",
                Empty: "The agent's answer was empty.",
                Unreachable: $"The agent could not be reached at {url}: ",
                NoAnswer: "The agent did not answer within",
                Unreadable: "The agent's answer could not be read as the contract's items: "
            ),
            turn => TurnResult.Succeeded(turn.Items),
            TurnResult.Failed,
            cancellationToken
        );
    }

    /// <summary>
    /// Sends the request and reads the answer as <typeparamref name="T"/>; whatever goes wrong becomes <paramref name="failed"/>
    /// with the sentence from <paramref name="messages"/>.
    /// </summary>
    private async Task<TResult> SendAsync<T, TResult>(
        HttpRequestMessage request,
        Messages messages,
        Func<T, TResult> succeeded,
        Func<string, TResult> failed,
        CancellationToken cancellationToken
    )
        where T : class
    {
        using (request)
        {
            try
            {
                using var response = await http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    var excerpt = body.Length > BodyExcerptLength ? body[..BodyExcerptLength] + "…" : body;
                    return failed($"{messages.Answered} {(int)response.StatusCode} {response.ReasonPhrase}: {excerpt}".TrimEnd(' ', ':'));
                }
                var answer = await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, cancellationToken);
                return answer is null ? failed(messages.Empty) : succeeded(answer);
            }
            catch (HttpRequestException error)
            {
                return failed($"{messages.Unreachable}{error.Message}");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return failed($"{messages.NoAnswer} {http.Timeout.TotalSeconds:0.##} s.");
            }
            catch (Exception error) when (error is JsonException or NotSupportedException)
            {
                return failed($"{messages.Unreadable}{error.Message}");
            }
        }
    }

    /// <summary>The sentences of the failures; a message that continues with the cause or a number ends where that begins.</summary>
    private sealed record Messages(string Answered, string Empty, string Unreachable, string NoAnswer, string Unreadable);
}
