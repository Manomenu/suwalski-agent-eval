using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Monitoring;

/// <summary>
/// What is wrong with the agent's monitoring checks (<c>suwageval/monitoring-checks.yaml</c>), written once for <c>validate</c>, <c>push</c>
/// and the server: every rule of <see cref="CheckRules"/>, and those of a check that runs once on each conversation of the agent — a
/// description, no scenario, no samples or pass threshold (a conversation is one sample), each key once. Paths are <c>checks[i]…</c>.
/// </summary>
public static class MonitoringCheckRules
{
    public static IReadOnlyList<CheckProblem> Problems(IReadOnlyList<CheckInput> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        var problems = new List<CheckProblem>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < checks.Count; i++)
        {
            var check = checks[i];
            var path = $"checks[{i}]";
            problems.AddRange(CheckRules.Problems(check, path));
            if (!keys.Add(check.Key))
            {
                problems.Add(new CheckProblem($"{path}.key", $"The key '{check.Key}' is there twice."));
            }
            if (string.IsNullOrWhiteSpace(check.Description))
            {
                problems.Add(
                    new CheckProblem($"{path}.description", "A monitoring check says what it makes sure of: a description in plain words.")
                );
            }
            if (check.ScenarioKey.Length > 0)
            {
                problems.Add(new CheckProblem($"{path}.scenarioKey", "A monitoring check belongs to the agent, not to a scenario."));
            }
            if (check.Samples is not null)
            {
                problems.Add(new CheckProblem($"{path}.samples", "A monitoring check runs once on each conversation: no samples."));
            }
            if (Math.Abs(check.PassThreshold - 1.0) > double.Epsilon)
            {
                problems.Add(
                    new CheckProblem($"{path}.passThreshold", "A monitoring check runs once on each conversation: no pass threshold.")
                );
            }
        }
        return problems;
    }
}
