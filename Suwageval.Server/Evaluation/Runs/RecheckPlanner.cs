using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Evaluation.Runs;

internal static class RecheckPlanner
{
    public static RecheckPlan GateRun() =>
        Blocked(
            RecheckBlocker.GateRun,
            "A gate run is not re-checked: its attempts stopped at the first success or failure. Run the gate again (suwageval gate).",
            [],
            0,
            [],
            []
        );

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
            return Blocked(RecheckBlocker.NotFinished, $"Only a finished run can be re-checked; this one is {status}.", [], 0, [], []);
        }

        var scope = RecheckScope.Of(originalSamples, scenarios);
        if (scope.Kept.Count == 0)
        {
            return Blocked(
                RecheckBlocker.AllSkipped,
                "Every scenario of the run changed or was deleted since, so its stored answers answer other conversations.",
                [],
                0,
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
        var judged = JudgedCount(changed);

        if (mismatches.Count > 0)
        {
            return Blocked(
                RecheckBlocker.Mismatches,
                "The current checks do not fit the version the run was under test.",
                changed,
                judged,
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
                judged,
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
        int judged,
        IReadOnlyList<SkippedScenario> skipped,
        IReadOnlyList<string> mismatches
    ) =>
        new()
        {
            Possible = false,
            Blocker = blocker,
            Reason = reason,
            ChangedChecks = changed,
            JudgedChecks = judged,
            SkippedScenarios = skipped,
            Mismatches = mismatches,
        };

    private static int JudgedCount(IReadOnlyList<ChangedCheck> changed) =>
        changed.Count(change => change.Change != CheckChange.Removed && change.After is { } after && CheckEvaluator.JudgedByModel(after));

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
            AfterJudgedByModel = after is not null && CheckEvaluator.JudgedByModel(after),
        };

    private static CheckInput Definition(CheckResultRow verdict) =>
        // Why: the verdict's definition was serialized by the server when the check ran, so it is valid JSON
        JsonSerializer.Deserialize<CheckInput>(verdict.Definition, ContractJson.Options)!;
}
