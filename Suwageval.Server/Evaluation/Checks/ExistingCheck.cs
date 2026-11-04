namespace Suwageval.Server.Evaluation.Checks;

internal sealed record ExistingCheck(Guid Id, bool Deleted, Guid ScenarioId, int Revision, string ContentHash);
