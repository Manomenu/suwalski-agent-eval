namespace Suwageval.Server.Agents.Versions;

internal readonly record struct VersionRunCounts(int Runs, int GroupRuns)
{
    public static VersionRunCounts None => new(0, 0);
}
