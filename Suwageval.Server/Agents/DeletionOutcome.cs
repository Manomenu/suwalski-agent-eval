namespace Suwageval.Server.Agents;

/// <summary>How a request to delete a version or an agent ended (NotArchived is for versions only).</summary>
internal enum DeletionOutcome
{
    Deleted,

    /// <summary>A dry run: it would be deleted.</summary>
    Allowed,
    NotFound,
    NotArchived,
    HasActiveRuns,
}
