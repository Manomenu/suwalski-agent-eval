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
