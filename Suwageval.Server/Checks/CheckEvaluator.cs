using System.Text.Json;
using System.Text.RegularExpressions;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;

namespace Suwageval.Server.Checks;

/// <summary>
/// The deterministic checks (docs/scenario-format.md, "Checks"): pure functions of a check and a trace, no I/O, no model.
/// Every verdict says in one sentence why, and carries the items that decided it.
/// </summary>
internal static class CheckEvaluator
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    public static CheckVerdict Evaluate(CheckInput check, Trace trace)
    {
        ArgumentNullException.ThrowIfNull(check);
        if (UsesJudge(check))
        {
            throw new ArgumentException("A check with $judge matchers needs the model: it goes through ArgumentJudge.", nameof(check));
        }
        var scope = CheckScope.Of(trace, check.Where);
        return check switch
        {
            ToolCalledCheck called => ToolCalled(called, scope),
            ToolNotCalledCheck notCalled => ToolNotCalled(notCalled, scope),
            EventCheck emitted => EventEmitted(emitted, scope),
            TextCheck text => Text(text, scope),
            _ => throw new ArgumentException($"No evaluator for {check.GetType().Name}.", nameof(check)),
        };
    }

    /// <summary>Whether the check's <c>args</c> / <c>data</c> hold a <c>$judge</c> matcher, so a model has the last word on it.</summary>
    public static bool UsesJudge(CheckInput check) =>
        check switch
        {
            ToolCalledCheck { Args: { } args } => JudgeMatcher.IsIn(args),
            EventCheck { Data: { } data } => JudgeMatcher.IsIn(data),
            _ => false,
        };

    /// <summary>
    /// The deterministic part of a check that <see cref="UsesJudge"/>: every literal must match as in <see cref="Evaluate"/>, a
    /// <c>$judge</c> node only needs its path to exist. What passed are the candidates a model then judges; null for any other check.
    /// </summary>
    public static ArgumentSearch? Search(CheckInput check, Trace trace)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(trace);
        var scope = CheckScope.Of(trace, check.Where);
        switch (check)
        {
            case ToolCalledCheck { Args: { } args } called when JudgeMatcher.IsIn(args):
                var calls = scope.Items.OfType<ToolCall>().Where(call => call.Name == called.Tool).ToList();
                return Candidates(
                    $"{called.Tool} was called{scope.Description}",
                    $"tool call {called.Tool}",
                    [.. calls.Select(call => (Item: (ConversationItem)call, Actual: call.Arguments))],
                    args,
                    "args",
                    calls.Count > 0
                        ? new CheckVerdict(
                            false,
                            calls,
                            $"{called.Tool} was called{scope.Description}, but not with arguments containing {args}."
                        )
                        : new CheckVerdict(false, [], $"{called.Tool} was not called{scope.Description}.")
                );
            case EventCheck { Data: { } data } emitted when JudgeMatcher.IsIn(data):
                var events = scope.Items.OfType<Event>().Where(item => item.Type == emitted.Event).ToList();
                return Candidates(
                    $"Event {emitted.Event} was emitted{scope.Description}",
                    $"event {emitted.Event}",
                    [.. events.Select(item => (Item: (ConversationItem)item, Actual: item.Data))],
                    data,
                    "data",
                    events.Count > 0
                        ? new CheckVerdict(
                            false,
                            events,
                            $"Event {emitted.Event} was emitted{scope.Description}, but not with data containing {data}."
                        )
                        : new CheckVerdict(false, [], $"Event {emitted.Event} was not emitted{scope.Description}.")
                );
            default:
                return null;
        }
    }

    private static ArgumentSearch Candidates(
        string subject,
        string context,
        IReadOnlyList<(ConversationItem Item, JsonElement Actual)> named,
        JsonElement expected,
        string root,
        CheckVerdict whenNoCandidate
    ) =>
        new(
            subject,
            context,
            [.. named.Select(pair => pair.Item)],
            [
                .. named
                    .Where(pair => JsonSubset.Contains(pair.Actual, expected))
                    .Select(pair => new ArgumentCandidate(pair.Item, JsonSubset.Judgements(pair.Actual, expected, root))),
            ],
            whenNoCandidate
        );

    private static CheckVerdict ToolCalled(ToolCalledCheck check, CheckScope scope)
    {
        var calls = scope.Items.OfType<ToolCall>().Where(call => call.Name == check.Tool).ToList();
        var matching = check.Args is { } args ? calls.FindAll(call => JsonSubset.Contains(call.Arguments, args)) : calls;
        if (matching.Count > 0)
        {
            return new CheckVerdict(true, matching, $"{check.Tool} was called{scope.Description}.");
        }
        return calls.Count > 0
            ? new CheckVerdict(false, calls, $"{check.Tool} was called{scope.Description}, but not with arguments containing {check.Args}.")
            : new CheckVerdict(false, [], $"{check.Tool} was not called{scope.Description}.");
    }

    private static CheckVerdict ToolNotCalled(ToolNotCalledCheck check, CheckScope scope)
    {
        var calls = scope.Items.OfType<ToolCall>().Where(call => call.Name == check.Tool).ToList();
        return calls.Count > 0
            ? new CheckVerdict(false, calls, $"{check.Tool} was called{scope.Description}.")
            : new CheckVerdict(true, [], $"{check.Tool} was not called{scope.Description}.");
    }

    private static CheckVerdict EventEmitted(EventCheck check, CheckScope scope)
    {
        var events = scope.Items.OfType<Event>().Where(emitted => emitted.Type == check.Event).ToList();
        var matching = check.Data is { } data ? events.FindAll(emitted => JsonSubset.Contains(emitted.Data, data)) : events;
        if (matching.Count > 0)
        {
            return new CheckVerdict(true, matching, $"Event {check.Event} was emitted{scope.Description}.");
        }
        return events.Count > 0
            ? new CheckVerdict(
                false,
                events,
                $"Event {check.Event} was emitted{scope.Description}, but not with data containing {check.Data}."
            )
            : new CheckVerdict(false, [], $"Event {check.Event} was not emitted{scope.Description}.");
    }

    /// <summary>The assistant messages in scope, joined by a newline, are "the answer"; every condition given must hold.</summary>
    private static CheckVerdict Text(TextCheck check, CheckScope scope)
    {
        var messages = scope.Items.OfType<AssistantMessage>().ToList();
        var answer = string.Join('\n', messages.Select(message => message.Text));
        var failures = new List<string>();
        if (check.Contains is { } contains && !answer.Contains(contains, StringComparison.Ordinal))
        {
            failures.Add($"does not contain \"{contains}\"");
        }
        if (check.NotContains is { } notContains && answer.Contains(notContains, StringComparison.Ordinal))
        {
            failures.Add($"contains \"{notContains}\"");
        }
        if (check.Pattern is { } pattern && !Matches(answer, pattern, out var problem))
        {
            failures.Add(problem ?? $"does not match /{pattern}/");
        }
        var subject = messages.Count == 0 ? "There is no answer" + scope.Description : "The answer" + scope.Description;
        return failures.Count == 0
            ? new CheckVerdict(true, messages, $"{subject} satisfies the text check.")
            : new CheckVerdict(false, messages, $"{subject} {string.Join(" and ", failures)}.");
    }

    private static bool Matches(string text, string pattern, out string? problem)
    {
        problem = null;
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.None, PatternTimeout);
        }
        catch (ArgumentException error)
        {
            problem = $"has an invalid pattern /{pattern}/ ({error.Message})";
        }
        catch (RegexMatchTimeoutException)
        {
            problem = $"took too long to match /{pattern}/";
        }
        return false;
    }
}
