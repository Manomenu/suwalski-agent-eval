namespace Suwageval.Server.Simulation;

/// <summary>The <c>Simulation</c> section of the configuration: the model that plays the simulated user, and how freely it writes.</summary>
internal sealed class SimulationOptions
{
    public const string Section = "Simulation";

    /// <summary>
    /// The name of a model under <c>Models</c> (committed: <c>judge</c>, the judge's own). Naming the judge's entry shares its client and
    /// so its limit of calls at once; another entry has a limit of its own. Simulated scenarios run only when that model is configured.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// The simulated user's temperature (committed: 0.7; <c>Simulation__Temperature</c> in the environment). Above 0 on purpose: the
    /// samples of a simulation should not all go the same way, and the pass threshold over several samples is what makes it repeatable.
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}
