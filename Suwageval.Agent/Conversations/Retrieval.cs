namespace Suwageval.Contracts.Conversations;

public sealed record Retrieval : ConversationItem
{
    public required string Query { get; init; }

    public required IReadOnlyList<RetrievedChunk> Chunks { get; init; }
}
