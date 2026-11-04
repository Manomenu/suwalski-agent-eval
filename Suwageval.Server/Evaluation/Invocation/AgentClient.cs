using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Invocation;

namespace Suwageval.Server.Evaluation.Invocation;

// Why: whatever goes wrong with the agent is returned as a failed result, because it belongs to that sample and must never stop a run
internal sealed class AgentClient(HttpClient http)
{
    private const int BodyExcerptLength = 300;

    // Why: nulls are left out because an agent that does not know a field must not be sent it
    private static readonly JsonSerializerOptions Request = new(ContractJson.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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

    internal static string Baggage(TurnContext context)
    {
        var members = new List<KeyValuePair<string, string>> { new(SuwagevalBaggage.RunId, context.RunId.ToString()) };
        if (context.AgentVersionId is { } versionId)
        {
            members.Add(new(SuwagevalBaggage.AgentVersionId, versionId.ToString()));
        }
        members.AddRange([
            new(SuwagevalBaggage.ScenarioKey, context.ScenarioKey),
            new(SuwagevalBaggage.ScenarioTypeKey, context.ScenarioType.ToString()),
            new(SuwagevalBaggage.ScenarioSample, context.Sample.ToString(CultureInfo.InvariantCulture)),
        ]);
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

    private sealed record Messages(string Answered, string Empty, string Unreachable, string NoAnswer, string Unreadable);
}
