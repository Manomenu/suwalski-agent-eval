namespace Suwageval.Server.Judges;

/// <summary>The <c>Judge</c> section of the configuration: which of the <c>Models</c> gives the verdicts of judge checks.</summary>
internal sealed class JudgeOptions
{
    public const string Section = "Judge";

    /// <summary>
    /// The name of a model under <c>Models</c> (committed: <c>judge</c>). Judge checks run only when that model is configured — its
    /// address and name come from the environment (<c>.env</c> on a laptop, the chart on the cluster), never from a committed file.
    /// </summary>
    public string? Model { get; set; }
}
