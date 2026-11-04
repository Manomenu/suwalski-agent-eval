namespace Suwageval.Server.Evaluation.Simulation;

internal sealed class SimulationOptions
{
    public const string Section = "Simulation";

    // Why: above 0 on purpose, so the samples of a simulation do not all go the same way and the pass threshold over several samples makes it repeatable
    public double Temperature { get; set; } = 0.7;
}
