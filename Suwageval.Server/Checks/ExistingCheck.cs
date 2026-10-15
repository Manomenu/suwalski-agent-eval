namespace Suwageval.Server.Checks;

/// <summary>What an upsert needs to know about a check already stored: its scenario, latest revision and whether it is deleted.</summary>
internal sealed record ExistingCheck(Guid Id, bool Deleted, Guid ScenarioId, int Revision, string ContentHash);
