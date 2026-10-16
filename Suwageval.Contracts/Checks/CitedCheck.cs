namespace Suwageval.Contracts.Checks;

public sealed record CitedCheck : CheckInput
{
    /// <summary>The ids of the documents the answer must be grounded on: every one must appear in the <c>sources</c> of some assistant message. At least one, none empty.</summary>
    public required IReadOnlyList<string> Sources { get; init; }
}
