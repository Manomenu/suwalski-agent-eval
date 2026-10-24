using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Samples;
using Suwageval.Server.Evaluation.Checks;

namespace Suwageval.Server.Evaluation.Runs;

/// <summary>How many samples a scenario is called for and which of them each check is judged on, from the three levels of <c>samples</c>.</summary>
internal static class SamplePlan
{
    /// <summary>The number of samples <paramref name="check"/> is judged on (sample numbers 1 to this).</summary>
    public static int Effective(CheckInput check, ScenarioContent scenario, int? agentSamples)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(scenario);
        return SampleSettings.Effective(check.Samples, scenario.Samples, agentSamples);
    }

    /// <summary>The agent calls of a scenario: the most samples any of its checks is judged on; a scenario without checks has its own.</summary>
    public static int Calls(ScenarioContent scenario, IReadOnlyList<(CheckRow Row, CheckInput Input)> checks, int? agentSamples)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(checks);
        return checks.Count == 0
            ? SampleSettings.Effective(null, scenario.Samples, agentSamples)
            : checks.Max(check => Effective(check.Input, scenario, agentSamples));
    }

    /// <summary>The checks that are judged on sample number <paramref name="sample"/>.</summary>
    public static IEnumerable<(CheckRow Row, CheckInput Input)> JudgedOn(
        int sample,
        ScenarioContent scenario,
        IReadOnlyList<(CheckRow Row, CheckInput Input)> checks,
        int? agentSamples
    ) => checks.Where(check => sample <= Effective(check.Input, scenario, agentSamples));
}
