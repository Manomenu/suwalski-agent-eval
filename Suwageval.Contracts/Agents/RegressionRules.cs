namespace Suwageval.Contracts.Agents;

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
