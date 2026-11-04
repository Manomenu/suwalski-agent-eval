using Suwageval.Contracts.Runs;
using Suwageval.Server.Evaluation.Scenarios;

namespace Suwageval.Server.Evaluation.Runs;

internal sealed record RecheckScope(IReadOnlyList<ScenarioRow> Kept, IReadOnlyList<SkippedScenario> Skipped)
{
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
