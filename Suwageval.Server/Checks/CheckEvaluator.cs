using System.Text.Json;
using System.Text.RegularExpressions;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;

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
            ToolCalledCheck called => ToolCalled(called, scope, trace),
            ToolNotCalledCheck notCalled => ToolNotCalled(notCalled, scope),
            EventCheck emitted => EventEmitted(emitted, scope),
            EventNotEmittedCheck notEmitted => EventNotEmitted(notEmitted, scope),
            CitedCheck cited => Cited(cited, scope),
            TextCheck text => Text(text, scope),
            _ => throw new ArgumentException($"No evaluator for {check.GetType().Name}.", nameof(check)),
        };
    }

    /// <summary>
    /// Whether the check's <c>args</c> / <c>result</c> / <c>data</c> hold a <c>$judge</c> matcher, so a model has the last word on it.
    /// (<c>event-not-emitted</c> never does: validation rejects it there.)
    /// </summary>
    public static bool UsesJudge(CheckInput check) =>
        check switch
        {
            ToolCalledCheck called => IsJudged(called.Args) || IsJudged(called.Result),
            EventCheck emitted => IsJudged(emitted.Data),
            _ => false,
        };

    /// <summary>Whether a model gives the verdict of the check: a <c>judge</c> check, or one with a <c>$judge</c> matcher (<see cref="UsesJudge"/>).</summary>
    public static bool JudgedByModel(CheckInput check) => check is JudgeCheck || UsesJudge(check);

    private static bool IsJudged(JsonElement? expected) => expected is { } json && CheckRules.HoldsJudge(json);

    /// <summary>
    /// The deterministic part of a check that <see cref="UsesJudge"/>: every literal and <c>$pattern</c> must match as in
    /// <see cref="Evaluate"/>, a <c>$judge</c> node only needs its path to exist (in <c>args</c> / <c>data</c>, or in the parsed
    /// <c>result</c> of the call). What passed are the candidates a model then judges; null for any other check.
    /// </summary>
    public static ArgumentSearch? Search(CheckInput check, Trace trace)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(trace);
        if (!UsesJudge(check))
        {
            return null;
        }
        var scope = CheckScope.Of(trace, check.Where);
        switch (check)
        {
            case ToolCalledCheck called:
                // A call made by another agent is never a candidate for a `by` check.
                var calls = scope.Items.OfType<ToolCall>().Where(call => call.Name == called.Tool && MadeBy(call, called.By)).ToList();
                return new ArgumentSearch(
                    $"{ToolLabel(called.Tool, called.By)} was called{scope.Description}",
                    $"tool call {called.Tool}",
                    calls,
                    [
                        .. calls
                            .Select(call => (Call: call, Result: ResultJson(ResultOf(call, trace))))
                            .Where(pair => Satisfies(called, pair.Call, pair.Result))
                            .Select(pair => new ArgumentCandidate(
                                pair.Call,
                                [
                                    .. called.Args is { } args ? JsonSubset.Judgements(pair.Call.Arguments, args, "args") : [],
                                    .. called.Result is { } expected && pair.Result is { } actual
                                        ? JsonSubset.Judgements(actual, expected, "result")
                                        : [],
                                ]
                            )),
                    ],
                    ToolCalled(called, scope, trace)
                );
            case EventCheck { Data: { } data } emitted:
                var events = scope.Items.OfType<Event>().Where(item => item.Type == emitted.Event).ToList();
                return new ArgumentSearch(
                    $"Event {emitted.Event} was emitted{scope.Description}",
                    $"event {emitted.Event}",
                    events,
                    [
                        .. events
                            .Where(item => JsonSubset.Contains(item.Data, data))
                            .Select(item => new ArgumentCandidate(item, JsonSubset.Judgements(item.Data, data, "data"))),
                    ],
                    EventEmitted(emitted, scope)
                );
            default:
                return null;
        }
    }

    /// <summary>The deterministic part of a <c>tool-called</c> check for one call: its arguments, and the result when one is asked for.</summary>
    private static bool Satisfies(ToolCalledCheck check, ToolCall call, JsonElement? result)
    {
        var argsMatch = check.Args is not { } args || JsonSubset.Contains(call.Arguments, args);
        var resultMatches = check.Result is not { } expected || (result is { } actual && JsonSubset.Contains(actual, expected));
        return argsMatch && resultMatches;
    }

    private static CheckVerdict ToolCalled(ToolCalledCheck check, CheckScope scope, Trace trace)
    {
        var named = scope.Items.OfType<ToolCall>().Where(call => call.Name == check.Tool).ToList();
        var calls = named.FindAll(call => MadeBy(call, check.By));
        var tool = ToolLabel(check.Tool, check.By);
        var withArgs = check.Args is { } args ? calls.FindAll(call => JsonSubset.Contains(call.Arguments, args)) : calls;
        var matching = check.Result is { } expected ? withArgs.FindAll(call => ResultMatches(ResultOf(call, trace), expected)) : withArgs;
        if (matching.Count > 0)
        {
            return new CheckVerdict(true, WithResults(matching, check, trace), $"{tool} was called{scope.Description}.");
        }
        if (calls.Count == 0 && named.Count > 0)
        {
            return new CheckVerdict(false, named, $"{check.Tool} was called{scope.Description}, but not by {check.By}.");
        }
        if (calls.Count == 0)
        {
            return new CheckVerdict(false, [], $"{tool} was not called{scope.Description}.");
        }
        if (withArgs.Count == 0)
        {
            return new CheckVerdict(false, calls, $"{tool} was called{scope.Description}, but not with arguments containing {check.Args}.");
        }
        var answered = withArgs.Any(call => ResultOf(call, trace) is not null);
        return new CheckVerdict(
            false,
            WithResults(withArgs, check, trace),
            answered
                ? $"{tool} was called{scope.Description}, but not with a result containing {check.Result}."
                : $"{tool} was called{scope.Description}, but it has no result."
        );
    }

    /// <summary>The call's answer: the first tool result with its id, looked for in the whole trace, not only the scope.</summary>
    private static ToolResult? ResultOf(ToolCall call, Trace trace) =>
        trace.Turns.SelectMany(turn => turn).OfType<ToolResult>().FirstOrDefault(result => result.CallId == call.Id);

    /// <summary>The result's content as JSON; content that is not JSON is the JSON string of itself. Null when there is no result.</summary>
    private static JsonElement? ResultJson(ToolResult? result)
    {
        if (result is null)
        {
            return null;
        }
        try
        {
            using var parsed = JsonDocument.Parse(result.Content);
            return parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(result.Content);
        }
    }

    private static bool ResultMatches(ToolResult? result, JsonElement expected) =>
        ResultJson(result) is { } actual && JsonSubset.Contains(actual, expected);

    /// <summary>With a <c>result</c> in the check, the evidence of a call includes the result that answered it.</summary>
    private static List<ConversationItem> WithResults(List<ToolCall> calls, ToolCalledCheck check, Trace trace) =>
        [
            .. calls.SelectMany(call =>
                check.Result is not null && ResultOf(call, trace) is { } result ? [call, result] : (ConversationItem[])[call]
            ),
        ];

    /// <summary>With <c>by</c>, only a call that names that agent counts; a call with no agent never does.</summary>
    private static bool MadeBy(ToolCall call, string? by) => by is null || call.Agent == by;

    private static string ToolLabel(string tool, string? by) => by is null ? tool : $"{tool} (by {by})";

    private static CheckVerdict ToolNotCalled(ToolNotCalledCheck check, CheckScope scope)
    {
        var calls = scope.Items.OfType<ToolCall>().Where(call => call.Name == check.Tool && MadeBy(call, check.By)).ToList();
        var tool = ToolLabel(check.Tool, check.By);
        return calls.Count > 0
            ? new CheckVerdict(false, calls, $"{tool} was called{scope.Description}.")
            : new CheckVerdict(true, [], $"{tool} was not called{scope.Description}.");
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

    private static CheckVerdict EventNotEmitted(EventNotEmittedCheck check, CheckScope scope)
    {
        var events = scope.Items.OfType<Event>().Where(emitted => emitted.Type == check.Event).ToList();
        var matching = check.Data is { } data ? events.FindAll(emitted => JsonSubset.Contains(emitted.Data, data)) : events;
        var what = check.Data is null ? $"Event {check.Event}" : $"Event {check.Event} with data containing {check.Data}";
        return matching.Count > 0
            ? new CheckVerdict(false, matching, $"{what} was emitted{scope.Description}.")
            : new CheckVerdict(true, [], $"{what} was not emitted{scope.Description}.");
    }

    /// <summary>Every listed source must appear in the <c>sources</c> of some assistant message in scope.</summary>
    private static CheckVerdict Cited(CitedCheck check, CheckScope scope)
    {
        var messages = scope.Items.OfType<AssistantMessage>().ToList();
        var citing = messages.FindAll(message => message.Sources is { } sources && sources.Any(check.Sources.Contains));
        var cited = citing.SelectMany(message => message.Sources ?? []).ToHashSet();
        var missing = check.Sources.Where(source => !cited.Contains(source)).Distinct().ToList();
        return missing.Count == 0
            ? new CheckVerdict(true, citing, $"The answer{scope.Description} cites {string.Join(", ", check.Sources)}.")
            : new CheckVerdict(false, citing, $"The answer{scope.Description} does not cite {string.Join(", ", missing)}.");
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
            ? new CheckVerdict(true, messages, $"{subject} {string.Join(" and ", TextConditions(check))}.")
            : new CheckVerdict(false, messages, $"{subject} {string.Join(" and ", failures)}.");
    }

    /// <summary>What a passing <c>text</c> check found, condition by condition: <c>contains "Hello" and matches /(?i)hi/</c>.</summary>
    private static IEnumerable<string> TextConditions(TextCheck check)
    {
        if (check.Contains is { } contains)
        {
            yield return $"contains \"{contains}\"";
        }
        if (check.NotContains is { } notContains)
        {
            yield return $"does not contain \"{notContains}\"";
        }
        if (check.Pattern is { } pattern)
        {
            yield return $"matches /{pattern}/";
        }
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
