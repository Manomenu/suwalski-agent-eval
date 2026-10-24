namespace Suwageval.Server.Agents.Versions;

/// <summary>A version's runs: those outside any group and those in a group. Both zero: the version has no run.</summary>
internal readonly record struct VersionRunCounts(int Runs, int GroupRuns)
{
    public static VersionRunCounts None => new(0, 0);
}
