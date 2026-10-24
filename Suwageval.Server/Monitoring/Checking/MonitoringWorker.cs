namespace Suwageval.Server.Monitoring.Checking;

/// <summary>
/// Checks the closed conversations, a few at a time, as they close: a hosted service of the server like the run worker (no component of
/// its own). A conversation that fails to be checked is tried again at the next look.
/// </summary>
internal sealed partial class MonitoringWorker(
    CheckingStore checking,
    ConversationChecker checker,
    TimeProvider time,
    ILogger<MonitoringWorker> logger
) : BackgroundService
{
    /// <summary>How often the worker looks for closed conversations.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private const int Batch = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                foreach (var (conversationId, agentId) in await checking.UncheckedAsync(Batch, stoppingToken))
                {
                    await checker.CheckAsync(conversationId, agentId, stoppingToken);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                LogFailed(error);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Checking closed conversations failed; trying again at the next look")]
    private partial void LogFailed(Exception error);
}
