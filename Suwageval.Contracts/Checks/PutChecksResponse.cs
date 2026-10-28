namespace Suwageval.Contracts.Checks;

/// <summary>What happened to each check of the request, in its order, then to each check it deleted.</summary>
public sealed record PutChecksResponse(IReadOnlyList<CheckResult> Results);
