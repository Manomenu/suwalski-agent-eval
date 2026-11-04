namespace Suwageval.Server.Agents;

internal enum DeletionOutcome
{
    Deleted,

    Allowed,
    NotFound,
    NotArchived,
    HasActiveRuns,
}
