namespace Suwageval.Contracts.Checks;

/// <summary>The checks to write for an agent; keys not listed are left alone.</summary>
public sealed record PutChecksRequest(IReadOnlyList<CheckInput> Checks);
