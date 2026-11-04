namespace Suwageval.Server.Evaluation.Invocation;

internal sealed class InvocationOptions
{
    public const string Section = "Invocation";

    public int TimeoutSeconds { get; set; } = 120;
}
