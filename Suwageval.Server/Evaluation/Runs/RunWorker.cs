using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Suwageval.Server.Evaluation.Runs;

/// <summary>
/// The queue's consumer, inside the server: takes the oldest queued run, carries it out, looks again; sleeps for
/// <see cref="RunOptions.PollInterval"/> when there is nothing to do. One run at a time per server — a run already fills its own degree of parallelism.
/// </summary>
internal sealed partial class RunWorker(
    RunStore runs,
    RunExecutor executor,
    RunRechecker rechecker,
    IOptions<RunOptions> options,
    ILogger<RunWorker> logger
) : BackgroundService
{
    private readonly TimeSpan _pollInterval = options.Value.PollInterval;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The loop must outlive a failed poll, e.g. a database restart."
    )]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var interrupted = await runs.InterruptRunningAsync(cancellationToken: stoppingToken);
            LogInterrupted(interrupted);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error)
        {
            LogPollFailed(error);
        }
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
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                LogPollFailed(error);
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Marked {Count} runs left running by the previous server as interrupted")]
    private partial void LogInterrupted(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Taking the next run from the queue failed")]
    private partial void LogPollFailed(Exception error);
}
