using Suwageval.Contracts.Samples;

namespace Suwageval.Contracts.Agents;

/// <summary>
/// What is wrong with the optional settings of an <see cref="AgentDefinition"/> (<c>samples</c>, <c>regression</c>), written once for
/// <c>suwageval validate</c> and <c>push</c> and for the server when a push arrives. Paths are relative to the agent definition.
/// </summary>
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
        problems.AddRange(RegressionRules.Problems(definition.Regression));
        return problems;
    }
}
