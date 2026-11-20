using Suwageval.Contracts.Samples;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Contracts.Agents;

public static class AgentRules
{
    public static IReadOnlyList<(string Path, string Message)> Problems(AgentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var problems = new List<(string Path, string Message)>();
        if (SampleRules.Problem(definition.Samples, "samples") is { } samples)
        {
            problems.Add(("samples", samples));
        }
        if (AcGuardrailRules.RetryLimitProblem(definition.AcGuardrailRetryLimit, "acGuardrailRetryLimit") is { } retryLimit)
        {
            problems.Add(("acGuardrailRetryLimit", retryLimit));
        }
        problems.AddRange(RegressionRules.Problems(definition.Regression));
        problems.AddRange(FeatureRules.Problems(definition.Features));
        return problems;
    }
}
