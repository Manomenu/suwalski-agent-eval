namespace Suwageval.Server.Evaluation.Scenarios;

/// <summary>What a replace did to one scenario.</summary>
internal enum ScenarioOutcome
{
    Created,
    Updated,
    Unchanged,

    /// <summary>A live scenario the list no longer has: deleted, its revisions stay.</summary>
    Deleted,
}
