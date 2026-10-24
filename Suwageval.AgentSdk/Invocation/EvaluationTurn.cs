namespace Suwageval.AgentSdk.Invocation;

/// <summary>
/// Whether the code running now answers a turn of an evaluation (<c>POST /suwageval/turn</c>): such a turn is Suwageval's own test,
/// never a conversation of the monitored environment, so the monitoring leaves it out.
/// </summary>
internal static class EvaluationTurn
{
    private static readonly AsyncLocal<bool> Current = new();

    public static bool IsCurrent => Current.Value;

    /// <summary>Marks the code that follows, in this async flow, as an evaluation turn until the result is disposed.</summary>
    public static IDisposable Enter()
    {
        Current.Value = true;
        return new Leave();
    }

    private sealed class Leave : IDisposable
    {
        public void Dispose() => Current.Value = false;
    }
}
