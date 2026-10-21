namespace Suwageval.Contracts.Runs;

/// <summary>What deleting an archived run group removes with it: the runs (re-checks of them included), their samples and the verdicts on those.</summary>
public sealed record RunGroupDeletion(string Name, int Runs, int Samples, int Verdicts);
