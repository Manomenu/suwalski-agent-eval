namespace Suwageval.AgentSdk.Invocation;

internal static class EvaluationTurn
{
    private static readonly AsyncLocal<bool> Current = new();

    public static bool IsCurrent => Current.Value;

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
