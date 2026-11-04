using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Evaluation.Runs;

internal sealed record RunPlacement(RunOrigin? Origin, bool Reference, string? ComparedWith)
{
    public RunPlacement AsExperiment() => this with { Reference = false, ComparedWith = null };

    public static RunPlacement Decide(RunOrigin? origin, RegressionSettings? settings)
    {
        if (origin is null)
        {
            return new RunPlacement(null, false, null);
        }
        var branches = (settings?.Branches ?? [origin.DefaultBranch ?? ""]).Where(branch => !string.IsNullOrEmpty(branch)).ToList();
        var reference = origin.Clean && origin.Pushed && origin.Branch is { } branch && branches.Contains(branch, StringComparer.Ordinal);
        var comparedWith =
            !string.IsNullOrEmpty(origin.CompareWith) ? origin.CompareWith
            : origin.Branch is { } own && branches.Contains(own, StringComparer.Ordinal) ? own
            : origin.TargetBranch is { } target && branches.Contains(target, StringComparer.Ordinal) ? target
            : branches.Count > 0 ? branches[0]
            : null;
        return new RunPlacement(origin, reference, comparedWith);
    }
}
