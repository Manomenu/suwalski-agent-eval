namespace Suwageval.Server.Evaluation.Runs.Groups;

/// <summary>A group of an agent's runs with how many runs and versions it has, when its newest run was made and when it was archived.</summary>
internal sealed record RunGroupRow(string Name, int Runs, int Versions, DateTime NewestRunAt, DateTime? ArchivedAt);
