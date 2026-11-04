using Suwageval.AgentSdk.Conversations;

namespace Suwageval.Server.Evaluation.Invocation;

internal sealed record TurnResult
{
    public IReadOnlyList<ConversationItem>? Items { get; private init; }

    public string? Error { get; private init; }

    public int? DurationMs { get; init; }

    public static TurnResult Succeeded(IReadOnlyList<ConversationItem> items) => new() { Items = items };

    public static TurnResult Failed(string error) => new() { Error = error };
}
