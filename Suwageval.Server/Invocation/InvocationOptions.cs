namespace Suwageval.Server.Invocation;

/// <summary>The <c>Invocation</c> section of the configuration: how the agent under test is called.</summary>
internal sealed class InvocationOptions
{
    public const string Section = "Invocation";

    /// <summary>How long one call to the agent may take before the sample counts as failed.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}
