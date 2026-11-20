namespace Suwageval.Contracts.Monitoring;

public sealed record ConversationList
{
    public required IReadOnlyList<ConversationSummary> Conversations { get; init; }

    public required int Page { get; init; }

    public required int TotalCount { get; init; }

    public required IReadOnlyList<string> Environments { get; init; }
}
