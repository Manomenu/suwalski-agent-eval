using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Results;

/// <summary>
/// What <c>results --failed-only</c> leaves out, by the same rules as the run page's "Hide passed checks": a check passed when it reached
/// its threshold and no sample of it was left unjudged; a scenario passed when it has checks, all of them passed and every sample was
/// answered. A failed check, a check not judged and a sample with an agent error stay.
/// </summary>
internal static class PassedFilter
{
    public static bool CheckPassed(CheckRunResult check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return check.Passed && !check.Samples.Any(sample => sample.NotJudged);
    }

    public static bool ScenarioPassed(ScenarioRunResult scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Checks.Count > 0
            && scenario.Checks.All(CheckPassed)
            && scenario.Samples.All(sample => sample.Status == SampleStatus.Done);
    }
}
