namespace Suwageval.Server.Tests;

internal sealed class InFlightCalls
{
    private readonly Lock sync = new();
    private readonly TaskCompletionSource released = Signal();
    private readonly List<(int Count, TaskCompletionSource Signal)> awaited = [];
    private int inFlight;
    private int peak;

    public int Peak
    {
        get
        {
            lock (sync)
            {
                return peak;
            }
        }
    }

    public Task EnterAsync()
    {
        lock (sync)
        {
            inFlight++;
            peak = Math.Max(peak, inFlight);
            foreach (var wait in awaited.Where(wait => peak >= wait.Count))
            {
                wait.Signal.TrySetResult();
            }
            return released.Task;
        }
    }

    public void Leave()
    {
        lock (sync)
        {
            inFlight--;
        }
    }

    public Task ReachedAsync(int count)
    {
        lock (sync)
        {
            if (peak >= count)
            {
                return Task.CompletedTask;
            }
            var signal = Signal();
            awaited.Add((count, signal));
            return signal.Task;
        }
    }

    public void Release() => released.TrySetResult();

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
