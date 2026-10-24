namespace Suwageval.Server.Evaluation.Scenarios;

/// <summary>One revision of a scenario's content.</summary>
internal sealed record ScenarioRevisionRow(int Revision, string Content, string ContentHash, DateTime CreatedAt);
