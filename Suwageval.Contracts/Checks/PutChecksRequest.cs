namespace Suwageval.Contracts.Checks;

public sealed record PutChecksRequest(IReadOnlyList<CheckInput> Checks);
