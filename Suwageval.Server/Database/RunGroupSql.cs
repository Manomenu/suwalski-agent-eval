namespace Suwageval.Server.Database;

internal static class RunGroupSql
{
    public const string EffectiveGroup = "COALESCE(r.run_group, (SELECT o.run_group FROM runs o WHERE o.id = r.recheck_of))";
}
