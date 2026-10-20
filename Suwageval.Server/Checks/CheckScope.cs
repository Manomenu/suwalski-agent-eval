using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;

namespace Suwageval.Server.Checks;

/// <summary>The part of a trace a check looks at, after its <c>where</c> was applied, and a phrase telling which part that was.</summary>
internal sealed record CheckScope(IReadOnlyList<ConversationItem> Items, string Description)
{
    /// <summary>
    /// Applies <paramref name="where"/> to the trace. The turn selector picks the turns (all, the last, or number n; a turn that
    /// was not produced leaves nothing); the turns' items are then laid out in one sequence in the order produced. Then:
    /// <c>after</c> keeps what follows the FIRST item that is a tool call named so or an event of that type, and <c>before</c>
    /// what precedes the LAST such item — "after / before at least one occurrence". The anchor itself is never kept.
    /// An anchor that does not occur leaves nothing.
    /// </summary>
    public static CheckScope Of(Trace trace, CheckWhere? where)
    {
        ArgumentNullException.ThrowIfNull(trace);
        var selector = where?.Turn ?? default;
        var description = new List<string>();
        IReadOnlyList<IReadOnlyList<ConversationItem>> turns;
        switch (selector.Kind)
        {
            case TurnSelectorKind.Last:
                turns = trace.Turns.Count == 0 ? [] : [trace.Turns[^1]];
                description.Add("in the last turn");
                break;
            case TurnSelectorKind.Number:
                turns = selector.Number <= trace.Turns.Count ? [trace.Turns[selector.Number - 1]] : [];
                description.Add(
                    selector.Number <= trace.Turns.Count
                        ? $"in turn {selector.Number}"
                        : $"in turn {selector.Number} (the agent produced {trace.Turns.Count})"
                );
                break;
            case TurnSelectorKind.Any:
            default:
                turns = trace.Turns;
                break;
        }

        var sequence = turns.SelectMany(turn => turn).ToList();
        var from = 0;
        var to = sequence.Count;
        if (where?.After is { } after)
        {
            var anchor = sequence.FindIndex(item => IsAnchor(item, after));
            from = anchor < 0 ? sequence.Count : anchor + 1;
            description.Add(anchor < 0 ? $"after {after} (which did not happen)" : $"after {after}");
        }
        if (where?.Before is { } before)
        {
            var anchor = sequence.FindLastIndex(item => IsAnchor(item, before));
            to = anchor < 0 ? 0 : anchor;
            description.Add(anchor < 0 ? $"before {before} (which did not happen)" : $"before {before}");
        }
        var items = from < to ? sequence.GetRange(from, to - from) : [];
        return new CheckScope(items, description.Count == 0 ? "" : " " + string.Join(", ", description));
    }

    private static bool IsAnchor(ConversationItem item, string name) =>
        item switch
        {
            ToolCall call => call.Name == name,
            Event emitted => emitted.Type == name,
            _ => false,
        };
}
