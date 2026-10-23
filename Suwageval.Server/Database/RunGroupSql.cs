namespace Suwageval.Server.Database;

/// <summary>The one SQL rule for "this run is in a group", shared by the runs list and the versions list so they cannot drift.</summary>
internal static class RunGroupSql
{
    /// <summary>
    /// The group a run counts as being in, for a <c>runs</c> row aliased <c>r</c>: its own, or, for a re-check, the group of the run it was made
    /// from. Null: in no group.
    /// </summary>
    public const string EffectiveGroup = "COALESCE(r.run_group, (SELECT o.run_group FROM runs o WHERE o.id = r.recheck_of))";
}
