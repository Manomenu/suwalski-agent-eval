namespace Suwageval.Server.Judges;

/// <summary>What the judge model must answer, as JSON: the schema the model is asked to follow, read strictly.</summary>
internal sealed record JudgeAnswer(bool Passed, string Reason);
