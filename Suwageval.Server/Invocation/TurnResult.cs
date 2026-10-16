using Suwageval.Contracts.Conversations;

namespace Suwageval.Server.Invocation;

/// <summary>What one call to the agent under test came to: the items it produced, or why there are none.</summary>
internal sealed record TurnResult
{
    public IReadOnlyList<ConversationItem>? Items { get; private init; }

    public string? Error { get; private init; }

    public static TurnResult Succeeded(IReadOnlyList<ConversationItem> items) => new() { Items = items };

    public static TurnResult Failed(string error) => new() { Error = error };
}
