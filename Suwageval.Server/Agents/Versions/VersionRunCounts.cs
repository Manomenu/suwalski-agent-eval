namespace Suwageval.Server.Agents.Versions;

/// <summary>A version's runs: those outside any group and those in a group. Both zero: the version has no run.</summary>
internal readonly record struct VersionRunCounts(int Runs, int GroupRuns)
{
    public static VersionRunCounts None => new(0, 0);

    /// <summary>The version has runs and every one is in a group.</summary>
    public bool GroupRunsOnly => Runs == 0 && GroupRuns > 0;
}
