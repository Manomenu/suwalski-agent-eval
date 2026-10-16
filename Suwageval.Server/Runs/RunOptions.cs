namespace Suwageval.Server.Runs;

/// <summary>The <c>Runs</c> section of the configuration.</summary>
internal sealed class RunOptions
{
    public const string Section = "Runs";

    /// <summary>
    /// How many samples of one run are in flight at once. The agent under test is a running system with a capacity of its
    /// own, which Suwageval cannot see — this is what protects it (TODO.md, "Parallel runs").
    /// </summary>
    public int MaxParallelScenarios { get; set; } = 2;
}
