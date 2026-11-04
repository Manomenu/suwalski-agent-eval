using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Samples;
using Suwageval.Server.Evaluation.Checks;

namespace Suwageval.Server.Evaluation.Runs;

internal static class SamplePlan
{
    public static int Effective(CheckInput check, ScenarioContent scenario, int? agentSamples)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(scenario);
        return SampleSettings.Effective(check.Samples, scenario.Samples, agentSamples);
    }

    public static int Calls(ScenarioContent scenario, IReadOnlyList<(CheckRow Row, CheckInput Input)> checks, int? agentSamples)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(checks);
        return checks.Count == 0
            ? SampleSettings.Effective(null, scenario.Samples, agentSamples)
            : checks.Max(check => Effective(check.Input, scenario, agentSamples));
    }

    public static IEnumerable<(CheckRow Row, CheckInput Input)> JudgedOn(
        int sample,
        ScenarioContent scenario,
        IReadOnlyList<(CheckRow Row, CheckInput Input)> checks,
        int? agentSamples
    ) => checks.Where(check => sample <= Effective(check.Input, scenario, agentSamples));
}
