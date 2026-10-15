namespace Suwageval.Contracts.Conversations;

public sealed record RetrievedChunk
{
    public required string Text { get; init; }

    public required string Source { get; init; }

    public required double Score { get; init; }
}
