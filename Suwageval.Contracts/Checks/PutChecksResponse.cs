namespace Suwageval.Contracts.Checks;

public sealed record PutChecksResponse(IReadOnlyList<CheckResult> Results);
