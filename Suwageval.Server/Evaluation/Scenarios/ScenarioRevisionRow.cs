namespace Suwageval.Server.Evaluation.Scenarios;

internal sealed record ScenarioRevisionRow(int Revision, string Content, string ContentHash, DateTime CreatedAt);
