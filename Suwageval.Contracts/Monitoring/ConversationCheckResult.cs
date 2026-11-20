using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Contracts.Monitoring;

public sealed record ConversationCheckResult
{
    public required string Key { get; init; }

    public required int Revision { get; init; }

    public required bool Passed { get; init; }

    public bool NotJudged { get; init; }

    public required string Message { get; init; }

    public IReadOnlyList<ConversationItem> Evidence { get; init; } = [];

    public string? JudgeModel { get; init; }
}
