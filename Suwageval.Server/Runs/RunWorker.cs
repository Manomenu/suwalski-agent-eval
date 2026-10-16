using System.Diagnostics.CodeAnalysis;

namespace Suwageval.Server.Runs;

/// <summary>
/// The queue's consumer, inside the server: takes the oldest queued run, carries it out, looks again; sleeps a second when
/// there is nothing to do. One run at a time per server — a run already fills its own degree of parallelism.
/// </summary>
internal sealed partial class RunWorker(RunStore runs, RunExecutor executor, RunRechecker rechecker, ILogger<RunWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The loop must outlive a failed poll, e.g. a database restart."
    )]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var run = await runs.ClaimNextAsync(cancellationToken: stoppingToken);
                if (run is not null)
                {
                    await (run.RecheckOf is null ? executor.ExecuteAsync(run, stoppingToken) : rechecker.ExecuteAsync(run, stoppingToken));
                    continue;
                }
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                LogPollFailed(error);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Taking the next run from the queue failed")]
    private partial void LogPollFailed(Exception error);
}
