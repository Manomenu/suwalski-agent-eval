namespace Suwageval.Server.Simulation;

/// <summary>The <c>Simulation</c> section of the configuration: how freely the simulated user writes (its model is the <c>Models:simulator</c> entry, else the judge's; see UserSimulator).</summary>
internal sealed class SimulationOptions
{
    public const string Section = "Simulation";

    /// <summary>
    /// The simulated user's temperature (committed: 0.7; <c>Simulation__Temperature</c> in the environment). Above 0 on purpose: the
    /// samples of a simulation should not all go the same way, and the pass threshold over several samples is what makes it repeatable.
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}
