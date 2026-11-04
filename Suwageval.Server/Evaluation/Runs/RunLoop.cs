using System.Diagnostics.CodeAnalysis;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs;

internal static class RunLoop
{
    internal sealed record Plan<T>(IReadOnlyList<T> Work, Func<T, Task> Process, Func<Task>? Verify = null, int? Total = null);

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
            await runs.SetTotalAsync(run.Id, plan.Total ?? plan.Work.Count, cancellationToken);

            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await Parallel.ForEachAsync(
                    plan.Work,
                    new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = stop.Token },
                    async (item, _) =>
                    {
                        // Why: items in flight finish with the outer token, because stopping only starts no new one
                        if (await runs.IsCancelRequestedAsync(run.Id, cancellationToken))
                        {
                            await stop.CancelAsync();
                            return;
                        }
                        await plan.Process(item);
                    }
                );
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
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
