namespace Suwageval.Contracts.Checks;

/// <summary>What is wrong with a check: where (<c>checks[2].args.zip.$pattern</c>) and what, in a sentence.</summary>
public sealed record CheckProblem(string Path, string Message);
