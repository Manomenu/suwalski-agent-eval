namespace Suwageval.Contracts.Monitoring;

/// <summary>One page of an agent's monitored conversations, the one with the newest turn first.</summary>
public sealed record ConversationList
{
    public required IReadOnlyList<ConversationSummary> Conversations { get; init; }

    /// <summary>The page these are (1-based): the one asked for, or the last one when that is past the end.</summary>
    public required int Page { get; init; }

    /// <summary>How many conversations match the filters, over all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>The environments the agent's conversations came from, sorted: what the environment filter offers.</summary>
    public required IReadOnlyList<string> Environments { get; init; }
}
