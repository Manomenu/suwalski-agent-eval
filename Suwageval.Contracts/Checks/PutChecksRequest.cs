namespace Suwageval.Contracts.Checks;

/// <summary>The agent's whole set of checks; a stored check not listed is deleted (its revisions and past verdicts stay).</summary>
public sealed record PutChecksRequest(IReadOnlyList<CheckInput> Checks);
