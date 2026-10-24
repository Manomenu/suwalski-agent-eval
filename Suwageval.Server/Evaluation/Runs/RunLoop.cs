using System.Diagnostics.CodeAnalysis;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs;

/// <summary>
/// The loop every kind of run shares (a run that calls the agent, a re-check): the total, the items in parallel, a poll for a
/// cancel request before each, and the final status — Done, Cancelled by request, or Failed with the error recorded. A shutdown
/// of the server is not a status: it leaves the run as it is, for the next start to find.
/// </summary>
internal static class RunLoop
{
    /// <summary>
    /// What a claimed run is going to do: the items to work through, what to do with one, and an optional step after the last item
    /// (before the run is finished).
    /// </summary>
    internal sealed record Plan<T>(IReadOnlyList<T> Work, Func<T, Task> Process, Func<Task>? Verify = null);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever breaks a run is recorded on it.")]
    public static async Task ExecuteAsync<T>(
        RunStore runs,
        int maxParallel,
        RunRow run,
        Func<Task<Plan<T>>> prepare,
        Action<Exception> logFailure,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(run);
        try
        {
            var plan = await prepare();
            await runs.SetTotalAsync(run.Id, plan.Work.Count, cancellationToken);

            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await Parallel.ForEachAsync(
                    plan.Work,
                    new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = stop.Token },
                    async (item, _) =>
                    {
                        // Items in flight finish with the outer token: stopping only starts no new one.
                        if (await runs.IsCancelRequestedAsync(run.Id, cancellationToken))
                        {
                            await stop.CancelAsync();
                            return;
                        }
                        await plan.Process(item);
                    }
                );
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Cancelled by request: the status is set below.
            }
            var cancelled = stop.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            if (plan.Verify is { } verify)
            {
                await verify();
            }
            await runs.FinishAsync(run.Id, cancelled ? RunStatus.Cancelled : RunStatus.Done, cancellationToken: CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            logFailure(error);
            await runs.FinishAsync(run.Id, RunStatus.Failed, error.Message, CancellationToken.None);
        }
    }
}
