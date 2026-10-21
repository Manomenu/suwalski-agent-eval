namespace Suwageval.Server.Runs;

/// <summary>A group of an agent's runs with how many runs and versions it has and when its newest run was made.</summary>
internal sealed record RunGroupRow(string Name, int Runs, int Versions, DateTime NewestRunAt);
