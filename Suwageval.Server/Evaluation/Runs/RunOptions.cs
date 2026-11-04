namespace Suwageval.Server.Evaluation.Runs;

internal sealed class RunOptions
{
    public const string Section = "Runs";

    public int MaxParallelScenarios { get; set; } = 2;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
}
