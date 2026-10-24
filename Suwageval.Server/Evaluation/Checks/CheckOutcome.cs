namespace Suwageval.Server.Evaluation.Checks;

/// <summary>What an upsert did to one check.</summary>
internal enum CheckOutcome
{
    Created,
    Updated,
    Unchanged,

    /// <summary>The scenario the check names does not exist (or is deleted); nothing was written.</summary>
    UnknownScenario,
}
