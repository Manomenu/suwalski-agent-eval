namespace Suwageval.Server.Evaluation.Checks;

/// <summary>What a replace did to one check.</summary>
internal enum CheckOutcome
{
    Created,
    Updated,
    Unchanged,

    /// <summary>The scenario the check names does not exist (or is deleted); nothing was written.</summary>
    UnknownScenario,

    /// <summary>A live check the list no longer has: deleted, its revisions stay.</summary>
    Deleted,
}
