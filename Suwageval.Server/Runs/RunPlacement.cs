using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>
/// Where a run stands among the runs of its agent, decided once when it is created: the origin it was started from, whether it is a
/// reference run (it makes the history of its branch) and the branch whose history it is compared with (none: every run of the agent).
/// </summary>
internal sealed record RunPlacement(RunOrigin? Origin, bool Reference, string? ComparedWith)
{
    /// <summary>The placement of a run started from <paramref name="origin"/> for an agent with these regression settings.</summary>
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
