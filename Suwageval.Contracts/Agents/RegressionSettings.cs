namespace Suwageval.Contracts.Agents;

/// <summary>
/// How an agent's regressions are found (<c>regression:</c> in <c>agent.yaml</c>), both parts optional. A check's history is its verdicts
/// over the newest reference runs: runs from one of <see cref="Branches"/>, with a clean tree and a pushed commit. Nobody marks a
/// baseline; the history is the baseline.
/// </summary>
public sealed record RegressionSettings
{
    /// <summary>The default for <see cref="History"/>.</summary>
    public const int DefaultHistory = 5;

    public const int MinHistory = 1;

    public const int MaxHistory = 20;

    /// <summary>The branches whose runs make the history, the main one first; none means the repository's default branch (from git).</summary>
    public IReadOnlyList<string>? Branches { get; init; }

    /// <summary>
    /// How many of the newest reference runs make a check's history (<see cref="DefaultHistory"/> when not set): a check is steadily
    /// passing after that many passing runs, and a failure becomes a known one after that many failing runs in a row.
    /// </summary>
    public int? History { get; init; }
}
