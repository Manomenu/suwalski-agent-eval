using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Contracts.Runs;

public sealed record CheckSampleResult
{
    public required int Sample { get; init; }

    public required bool Passed { get; init; }

    public required bool NotJudged { get; init; }

    public required IReadOnlyList<ConversationItem> Evidence { get; init; }

    public required string Message { get; init; }
}
