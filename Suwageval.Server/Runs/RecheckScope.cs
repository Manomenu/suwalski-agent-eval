using Suwageval.Contracts.Runs;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>
/// Which scenarios of a source run a re-check evaluates: those whose latest revision is the one the source ran, so that its stored
/// answers still answer the scenario's conversation. A scenario that changed or was deleted since is skipped, with the reason.
/// </summary>
internal sealed record RecheckScope(IReadOnlyList<ScenarioRow> Kept, IReadOnlyList<SkippedScenario> Skipped)
{
    /// <param name="samples">The source run's samples.</param>
    /// <param name="live">The agent's live scenarios with their latest revision.</param>
    public static RecheckScope Of(IReadOnlyList<ScenarioResultRow> samples, IReadOnlyList<ScenarioRow> live)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(live);
        var current = live.ToDictionary(scenario => scenario.Id);
        var kept = new List<ScenarioRow>();
        var skipped = new List<SkippedScenario>();
        foreach (
            var ran in samples.GroupBy(sample => sample.ScenarioId).OrderBy(group => group.First().ScenarioKey, StringComparer.Ordinal)
        )
        {
            var first = ran.First();
            if (!current.TryGetValue(first.ScenarioId, out var latest))
            {
                skipped.Add(new SkippedScenario { Key = first.ScenarioKey, Reason = "The scenario was deleted after the run." });
            }
            else if (latest.Revision != first.ScenarioRevision)
            {
                skipped.Add(
                    new SkippedScenario
                    {
                        Key = first.ScenarioKey,
                        Reason =
                            $"The scenario changed after the run (revision {first.ScenarioRevision}, now {latest.Revision}): its stored answers answer another conversation.",
                    }
                );
            }
            else
            {
                kept.Add(latest);
            }
        }
        return new RecheckScope(kept, skipped);
    }
}
