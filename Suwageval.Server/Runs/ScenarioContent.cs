using System.Text.Json;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>The part of a stored scenario revision a run needs: what the agent is shown and how often.</summary>
internal sealed record ScenarioContent
{
    public required IReadOnlyList<ConversationItem> Conversation { get; init; }

    /// <summary>Sent verbatim with every turn; null when the scenario has none.</summary>
    public JsonElement? Fixtures { get; init; }

    public Continuation Continuation { get; init; } = new();

    /// <summary>Not set (null): the agent's default, else 3.</summary>
    public int? Samples { get; init; }
}
