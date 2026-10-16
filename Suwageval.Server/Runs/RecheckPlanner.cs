using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Checks;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>
/// Decides what a re-check of a run would do and whether it may start — for the preview and for the request that starts it.
/// Pure: everything it needs is passed in. A re-check copies the verdict of every check whose revision judged the run, so it only
/// changes something when a check changed, was added or was removed in the scenarios that are re-checked.
/// </summary>
internal static class RecheckPlanner
{
    /// <param name="status">The status of the run the person looked at.</param>
    /// <param name="originalSamples">The samples of the run that called the agent: its answers are what is re-checked.</param>
    /// <param name="shownSamples">The samples of the run the person looked at (the same, unless that is a re-check).</param>
    /// <param name="shownVerdicts">The verdicts of that run: what "changed since" is measured against.</param>
    /// <param name="scenarios">The agent's live scenarios.</param>
    /// <param name="checks">The agent's live checks.</param>
    /// <param name="version">The version the run was under test.</param>
    public static RecheckPlan Of(
        RunStatus status,
        IReadOnlyList<ScenarioResultRow> originalSamples,
        IReadOnlyList<ScenarioResultRow> shownSamples,
        IReadOnlyList<CheckResultRow> shownVerdicts,
        IReadOnlyList<ScenarioRow> scenarios,
        IReadOnlyList<CheckRow> checks,
        AgentContract version
    )
    {
        ArgumentNullException.ThrowIfNull(originalSamples);
        ArgumentNullException.ThrowIfNull(shownSamples);
        ArgumentNullException.ThrowIfNull(shownVerdicts);
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(version);
        if (status != RunStatus.Done)
        {
            return Blocked(RecheckBlocker.NotFinished, $"Only a finished run can be re-checked; this one is {status}.", [], [], []);
        }

        var scope = RecheckScope.Of(originalSamples, scenarios);
        if (scope.Kept.Count == 0)
        {
            return Blocked(
                RecheckBlocker.AllSkipped,
                "Every scenario of the run changed or was deleted since, so its stored answers answer other conversations.",
                [],
                scope.Skipped,
                []
            );
        }

        var kept = scope.Kept.ToDictionary(scenario => scenario.Key);
        var current = checks.Where(check => kept.ContainsKey(check.ScenarioKey)).ToList();
        var mismatches = RunCompatibility.Mismatches(
            version,
            [.. scope.Kept.Select(scenario => (scenario.Key, StoredContent.Scenario(scenario.Content)))],
            [.. current.Select(StoredContent.Check)]
        );
        HashSet<Guid> keptIds = [.. scope.Kept.Select(scenario => scenario.Id)];
        var changed = Changes(shownSamples, shownVerdicts, keptIds, current);
        var judged = changed.Count(change => change.Change != CheckChange.Removed && change.After is { } after && UsesJudge(after));

        if (mismatches.Count > 0)
        {
            return Blocked(
                RecheckBlocker.Mismatches,
                "The current checks do not fit the version the run was under test.",
                changed,
                scope.Skipped,
                mismatches
            );
        }
        if (changed.Count == 0)
        {
            return Blocked(
                RecheckBlocker.NothingChanged,
                "No check of the run's scenarios changed since the run, so a re-check would give the same results.",
                changed,
                scope.Skipped,
                []
            );
        }
        return new RecheckPlan
        {
            Possible = true,
            ChangedChecks = changed,
            JudgedChecks = judged,
            SkippedScenarios = scope.Skipped,
            Mismatches = [],
        };
    }

    private static RecheckPlan Blocked(
        RecheckBlocker blocker,
        string reason,
        IReadOnlyList<ChangedCheck> changed,
        IReadOnlyList<SkippedScenario> skipped,
        IReadOnlyList<string> mismatches
    ) =>
        new()
        {
            Possible = false,
            Blocker = blocker,
            Reason = reason,
            ChangedChecks = changed,
            JudgedChecks = changed.Count(change => change.Change != CheckChange.Removed && change.After is { } after && UsesJudge(after)),
            SkippedScenarios = skipped,
            Mismatches = mismatches,
        };

    private static bool UsesJudge(CheckInput check) => check is JudgeCheck || CheckEvaluator.UsesJudge(check);

    /// <summary>
    /// The checks of the re-checked scenarios (those the shown run has samples of) that differ from the verdicts of the shown run:
    /// another revision, no verdict yet, or a verdict of a check that is not live any more.
    /// </summary>
    private static List<ChangedCheck> Changes(
        IReadOnlyList<ScenarioResultRow> shownSamples,
        IReadOnlyList<CheckResultRow> shownVerdicts,
        HashSet<Guid> keptIds,
        IReadOnlyList<CheckRow> current
    )
    {
        var comparable = shownSamples.Where(sample => keptIds.Contains(sample.ScenarioId)).ToList();
        var scenarioOfSample = comparable.ToDictionary(sample => sample.Id, sample => sample.ScenarioKey);
        var comparableKeys = comparable.Select(sample => sample.ScenarioKey).ToHashSet();
        var ran = shownVerdicts
            .Where(verdict => scenarioOfSample.ContainsKey(verdict.ScenarioResultId))
            .GroupBy(verdict => (Scenario: scenarioOfSample[verdict.ScenarioResultId], verdict.CheckId))
            .ToDictionary(group => group.Key, group => group.First());
        var changes = new List<ChangedCheck>();
        var liveIds = current.Select(check => check.Id).ToHashSet();
        foreach (var check in current.Where(check => comparableKeys.Contains(check.ScenarioKey)))
        {
            var after = StoredContent.Check(check);
            if (!ran.TryGetValue((check.ScenarioKey, check.Id), out var verdict))
            {
                changes.Add(Change(check.ScenarioKey, check.Key, CheckChange.Added, null, after));
            }
            else if (verdict.CheckRevision != check.Revision)
            {
                changes.Add(Change(check.ScenarioKey, check.Key, CheckChange.Changed, Definition(verdict), after));
            }
        }
        changes.AddRange(
            ran.Where(entry => !liveIds.Contains(entry.Key.CheckId))
                .Select(entry => Change(entry.Key.Scenario, entry.Value.CheckKey, CheckChange.Removed, Definition(entry.Value), null))
        );
        return
        [
            .. changes
                .OrderBy(change => change.ScenarioKey, StringComparer.Ordinal)
                .ThenBy(change => change.CheckKey, StringComparer.Ordinal),
        ];
    }

    private static ChangedCheck Change(string scenarioKey, string checkKey, CheckChange change, CheckInput? before, CheckInput? after) =>
        new()
        {
            ScenarioKey = scenarioKey,
            CheckKey = checkKey,
            Change = change,
            Before = before,
            After = after,
        };

    private static CheckInput Definition(CheckResultRow verdict) =>
        JsonSerializer.Deserialize<CheckInput>(verdict.Definition, ContractJson.Options)!;
}
