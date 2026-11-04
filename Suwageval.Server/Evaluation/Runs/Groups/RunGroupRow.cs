namespace Suwageval.Server.Evaluation.Runs.Groups;

internal sealed record RunGroupRow(string Name, int Runs, int Versions, DateTime NewestRunAt, DateTime? ArchivedAt);
