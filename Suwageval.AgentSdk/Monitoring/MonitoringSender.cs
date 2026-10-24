using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Invocation;

namespace Suwageval.AgentSdk.Monitoring;

/// <summary>
/// Empties the queue of recorded turns into <c>POST /api/monitoring/turns</c>, a batch at a time, in the background. A batch that does
/// not get through is tried again later (the queue keeps filling meanwhile, dropping its oldest turns when full); one the server refuses
/// (a 4xx: the agent's setup is wrong, a retry would not help) is dropped with a warning in the log.
/// </summary>
internal sealed partial class MonitoringSender(
    Channel<MonitoredTurn> queue,
    IHttpClientFactory clients,
    IAgentContractSource contracts,
    IOptions<MonitoringOptions> options,
    ILogger<MonitoringSender> logger,
    TimeProvider time
) : BackgroundService
{
    /// <summary>How many turns one request carries at most.</summary>
    public const int MaxBatch = 50;

    /// <summary>How long the sender waits after the first turn for more to go in the same request.</summary>
    public static readonly TimeSpan Gather = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LastRetry = TimeSpan.FromMinutes(1);

    /// <summary>How long the sender may take, when the agent stops, to send what is still queued.</summary>
    private static readonly TimeSpan Flush = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                await Task.Delay(Gather, time, stoppingToken);
                await SendWithRetriesAsync(Take(), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The agent stops: send what is queued, once, within a short time.
            using var flush = new CancellationTokenSource(Flush);
            try
            {
                while (Take() is { Count: > 0 } rest)
                {
                    await SendAsync(rest, flush.Token);
                }
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
            {
                LogUnsent(error.Message);
            }
        }
    }

    private List<MonitoredTurn> Take()
    {
        var batch = new List<MonitoredTurn>();
        while (batch.Count < MaxBatch && queue.Reader.TryRead(out var turn))
        {
            batch.Add(turn);
        }
        return batch;
    }

    private async Task SendWithRetriesAsync(List<MonitoredTurn> batch, CancellationToken cancellationToken)
    {
        var wait = FirstRetry;
        while (true)
        {
            try
            {
                if (await SendAsync(batch, cancellationToken))
                {
                    return;
                }
            }
            catch (HttpRequestException error)
            {
                LogRetry(error.Message);
            }
            catch (TaskCanceledException error) when (!cancellationToken.IsCancellationRequested)
            {
                LogRetry(error.Message); // the request's own timeout
            }
            await Task.Delay(wait, time, cancellationToken);
            wait = wait * 2 > LastRetry ? LastRetry : wait * 2;
        }
    }

    /// <summary>True when the batch is done with (stored, or refused for good); false when it is worth trying again.</summary>
    private async Task<bool> SendAsync(List<MonitoredTurn> batch, CancellationToken cancellationToken)
    {
        var contract = await contracts.GetContractAsync(cancellationToken);
        var settings = options.Value;
        var request = new TurnBatch
        {
            Agent = settings.Agent ?? "",
            Environment = settings.Environment ?? "",
            ContractHash = AgentContractHash.Of(contract),
            Turns = batch,
        };
        using var client = clients.CreateClient(SuwagevalMonitoring.HttpClientName);
        var answer = await PostAsync(client, request, cancellationToken);
        if (answer is { SendContract: true })
        {
            answer = await PostAsync(client, request with { Contract = contract }, cancellationToken);
        }
        return answer is not null;
    }

    private async Task<TurnBatchAnswer?> PostAsync(HttpClient client, TurnBatch batch, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("monitoring/turns", UriKind.Relative),
            batch,
            ContractJson.Options,
            cancellationToken
        );
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<TurnBatchAnswer>(ContractJson.Options, cancellationToken);
        }
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (
            (int)response.StatusCode is >= 400 and < 500
            && response.StatusCode is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
        )
        {
            LogRefused((int)response.StatusCode, batch.Turns.Count, body);
            return new TurnBatchAnswer { Stored = 0 };
        }
        LogRetryStatus((int)response.StatusCode, body);
        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Suwageval refused {Turns} monitored turns ({Status}): {Body}")]
    private partial void LogRefused(int status, int turns, string body);

    [LoggerMessage(Level = LogLevel.Information, Message = "Monitored turns not sent yet, trying again: {Reason}")]
    private partial void LogRetry(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Monitored turns not sent yet, trying again: {Status} {Body}")]
    private partial void LogRetryStatus(int status, string body);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Monitored turns left unsent at shutdown: {Reason}")]
    private partial void LogUnsent(string reason);
}
