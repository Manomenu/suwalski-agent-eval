namespace Suwageval.Cli.Versions;

/// <summary>Where a version is used: the kinds of usage <c>suwageval versions --usage</c> filters by (the web's Versions page has the same five).</summary>
internal enum VersionUsage
{
    /// <summary>It has runs in no group.</summary>
    Evaluation,

    /// <summary>It has runs in a group.</summary>
    Groups,

    /// <summary>It answered conversations of the deployed agent (monitoring).</summary>
    Monitoring,

    /// <summary>It has no run and answered no conversation.</summary>
    Unused,

    /// <summary>It is archived.</summary>
    Archived,
}
