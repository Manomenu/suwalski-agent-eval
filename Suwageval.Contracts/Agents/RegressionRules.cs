namespace Suwageval.Contracts.Agents;

/// <summary>
/// Whether an agent's <see cref="RegressionSettings"/> are valid, written once for <c>suwageval validate</c> and <c>push</c> and for the
/// server when a push arrives. Paths are relative to the agent definition (<c>regression.history</c>).
/// </summary>
public static class RegressionRules
{
    public static IReadOnlyList<(string Path, string Message)> Problems(RegressionSettings? settings)
    {
        var problems = new List<(string Path, string Message)>();
        if (settings is null)
        {
            return problems;
        }
        if (settings.Branches is { } branches)
        {
            if (branches.Count == 0)
            {
                problems.Add(
                    ("regression.branches", "regression.branches names at least one branch; leave it out for the default branch.")
                );
            }
            for (var i = 0; i < branches.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(branches[i]) || branches[i].Any(char.IsWhiteSpace))
                {
                    problems.Add(($"regression.branches[{i}]", "A branch name is not empty and has no whitespace."));
                }
                else if (branches.Take(i).Contains(branches[i], StringComparer.Ordinal))
                {
                    problems.Add(($"regression.branches[{i}]", $"The branch '{branches[i]}' is named twice."));
                }
            }
        }
        if (settings.History is { } history and (< RegressionSettings.MinHistory or > RegressionSettings.MaxHistory))
        {
            problems.Add(
                (
                    "regression.history",
                    $"regression.history is from {RegressionSettings.MinHistory} to {RegressionSettings.MaxHistory} runs (the default is {RegressionSettings.DefaultHistory})."
                )
            );
        }
        return problems;
    }
}
