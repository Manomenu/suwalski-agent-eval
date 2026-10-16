using System.Text.Json;
using System.Text.RegularExpressions;

namespace Suwageval.Contracts.Checks;

/// <summary>
/// The rules that decide whether a check is valid, written once for everyone who validates one: <c>suwageval validate</c> and
/// <c>push</c> (the CLI) and the server when a push arrives. They look at one check at a time; what needs more than the check
/// (keys unique across the request or the files, tools and events an agent declares) stays with the caller, which can ask
/// <see cref="NamedTool"/>, <see cref="NamedEvent"/> and <see cref="Anchors"/> what the check refers to.
/// Paths are relative to the check, so a caller passes where the check sits (<c>checks[2]</c>).
/// </summary>
public static class CheckRules
{
    public const string JudgeKey = "$judge";

    private const string PatternKey = "$pattern";
    private const string Description = "description";
    private const string Examples = "examples";
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Everything wrong with the check itself, each problem with its path under <paramref name="path"/>.</summary>
    public static IReadOnlyList<CheckProblem> Problems(CheckInput check, string path)
    {
        ArgumentNullException.ThrowIfNull(check);
        var problems = new List<CheckProblem>();
        if (!Kebab.IsValid(check.Key))
        {
            problems.Add(
                new CheckProblem($"{path}.key", $"The check key '{check.Key}' is not lowercase kebab-case (like 'refund-happy-path').")
            );
        }
        if (check.PassThreshold is <= 0 or > 1 || double.IsNaN(check.PassThreshold))
        {
            problems.Add(new CheckProblem($"{path}.passThreshold", "passThreshold is above 0 and at most 1."));
        }

        switch (check)
        {
            case ToolCalledCheck called:
                AddMatcherProblems(problems, called.Args, $"{path}.args");
                AddMatcherProblems(problems, called.Result, $"{path}.result");
                break;
            case ToolNotCalledCheck:
                break;
            case EventCheck emitted:
                AddMatcherProblems(problems, emitted.Data, $"{path}.data");
                break;
            case EventNotEmittedCheck notEmitted:
                AddMatcherProblems(problems, notEmitted.Data, $"{path}.data");
                if (notEmitted.Data is { } data && HoldsJudge(data))
                {
                    // "No event whose data a model would accept" would need a model call per event and turn its verdict upside down.
                    problems.Add(
                        new CheckProblem(
                            $"{path}.data",
                            $"'{JudgeKey}' is not allowed in an event-not-emitted check; use a $pattern, or a literal value."
                        )
                    );
                }
                break;
            case CitedCheck cited:
                AddCitedProblems(problems, cited, path);
                break;
            case TextCheck text:
                AddTextProblems(problems, text, path);
                break;
            case JudgeCheck judge:
                if (string.IsNullOrWhiteSpace(judge.Criteria))
                {
                    problems.Add(
                        new CheckProblem($"{path}.criteria", "A judge check needs 'criteria': what the agent's output must satisfy.")
                    );
                }
                break;
            default:
                throw new ArgumentException($"No rules for {check.GetType().Name}.", nameof(check));
        }
        return problems;
    }

    /// <summary>The tool the check asks for (<c>tool-called</c>, <c>tool-not-called</c>); null for any other check.</summary>
    public static ToolReference? NamedTool(CheckInput check) =>
        check switch
        {
            ToolCalledCheck called => new ToolReference(called.Tool, called.By),
            ToolNotCalledCheck notCalled => new ToolReference(notCalled.Tool, notCalled.By),
            _ => null,
        };

    /// <summary>The event type the check asks for (<c>event</c>, <c>event-not-emitted</c>); null for any other check.</summary>
    public static string? NamedEvent(CheckInput check) =>
        check switch
        {
            EventCheck emitted => emitted.Event,
            EventNotEmittedCheck notEmitted => notEmitted.Event,
            _ => null,
        };

    /// <summary>The names in <c>where.after</c> / <c>where.before</c> — each a tool or an event type — with their path under the check.</summary>
    public static IEnumerable<(string Path, string Name)> Anchors(CheckInput check)
    {
        ArgumentNullException.ThrowIfNull(check);
        if (check.Where?.After is { } after)
        {
            yield return ("where.after", after);
        }
        if (check.Where?.Before is { } before)
        {
            yield return ("where.before", before);
        }
    }

    /// <summary>Whether a <c>$judge</c> key is anywhere in <paramref name="expected"/>.</summary>
    public static bool HoldsJudge(JsonElement expected) =>
        expected.ValueKind switch
        {
            JsonValueKind.Object => expected.TryGetProperty(JudgeKey, out _)
                || expected.EnumerateObject().Any(property => HoldsJudge(property.Value)),
            JsonValueKind.Array => expected.EnumerateArray().Any(HoldsJudge),
            JsonValueKind.Undefined
            or JsonValueKind.String
            or JsonValueKind.Number
            or JsonValueKind.True
            or JsonValueKind.False
            or JsonValueKind.Null => false,
            _ => false,
        };

    /// <summary>The matchers in <c>args</c> / <c>result</c> / <c>data</c>: every <c>$pattern</c> compiles, every <c>$judge</c> is well formed.</summary>
    private static void AddMatcherProblems(List<CheckProblem> problems, JsonElement? expected, string path)
    {
        if (expected is not { } json)
        {
            return;
        }
        AddPatternProblems(problems, json, path);
        AddJudgeProblems(problems, json, path);
    }

    private static void AddPatternProblems(List<CheckProblem> problems, JsonElement node, string path)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object when node.EnumerateObject().ToList() is [{ Name: PatternKey } only]:
                if (only.Value.ValueKind == JsonValueKind.String && CompileError(only.Value.GetString() ?? "") is { } error)
                {
                    problems.Add(new CheckProblem($"{path}.{PatternKey}", $"The pattern is not a valid regular expression: {error}"));
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                {
                    AddPatternProblems(problems, property.Value, $"{path}.{property.Name}");
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var element in node.EnumerateArray())
                {
                    AddPatternProblems(problems, element, $"{path}[{index++}]");
                }
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                break;
        }
    }

    private static void AddJudgeProblems(List<CheckProblem> problems, JsonElement node, string path)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object when node.TryGetProperty(JudgeKey, out var spec):
                if (node.EnumerateObject().Count() > 1)
                {
                    problems.Add(new CheckProblem(path, $"'{JudgeKey}' must be the only property of its object; this one has others."));
                }
                else
                {
                    AddJudgeSpecProblems(problems, spec, $"{path}.{JudgeKey}");
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                {
                    AddJudgeProblems(problems, property.Value, $"{path}.{property.Name}");
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var element in node.EnumerateArray())
                {
                    AddJudgeProblems(problems, element, $"{path}[{index++}]");
                }
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                break;
        }
    }

    private static void AddJudgeSpecProblems(List<CheckProblem> problems, JsonElement spec, string path)
    {
        if (spec.ValueKind != JsonValueKind.Object)
        {
            problems.Add(new CheckProblem(path, $"'{JudgeKey}' is an object with '{Description}' and/or '{Examples}'."));
            return;
        }
        foreach (var property in spec.EnumerateObject().Where(property => property.Name is not (Description or Examples)))
        {
            problems.Add(new CheckProblem(path, $"'{JudgeKey}' has no key '{property.Name}' (allowed: {Description}, {Examples})."));
        }
        var hasDescription = spec.TryGetProperty(Description, out var description);
        var hasExamples = spec.TryGetProperty(Examples, out var examples);
        if (!hasDescription && !hasExamples)
        {
            problems.Add(new CheckProblem(path, $"'{JudgeKey}' needs '{Description}' and/or '{Examples}'."));
        }
        if (hasDescription && (description.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(description.GetString())))
        {
            problems.Add(new CheckProblem($"{path}.{Description}", "The description is a non-empty string."));
        }
        if (hasExamples && (examples.ValueKind != JsonValueKind.Array || examples.GetArrayLength() == 0))
        {
            problems.Add(new CheckProblem($"{path}.{Examples}", "The examples are a non-empty list of values."));
        }
    }

    private static void AddCitedProblems(List<CheckProblem> problems, CitedCheck cited, string path)
    {
        if (cited.Sources.Count == 0)
        {
            problems.Add(new CheckProblem($"{path}.sources", "A cited check needs at least one source."));
        }
        for (var i = 0; i < cited.Sources.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(cited.Sources[i]))
            {
                problems.Add(new CheckProblem($"{path}.sources[{i}]", "A source is not empty."));
            }
        }
    }

    private static void AddTextProblems(List<CheckProblem> problems, TextCheck text, string path)
    {
        if (text.Contains is null && text.NotContains is null && text.Pattern is null)
        {
            problems.Add(new CheckProblem(path, "A text check needs at least one of 'contains', 'notContains', 'pattern'."));
        }
        if (text.Pattern is not null && CompileError(text.Pattern) is { } error)
        {
            problems.Add(new CheckProblem($"{path}.pattern", $"The pattern is not a valid regular expression: {error}"));
        }
    }

    private static string? CompileError(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, PatternTimeout);
            return null;
        }
        catch (ArgumentException error)
        {
            return error.Message;
        }
    }
}
