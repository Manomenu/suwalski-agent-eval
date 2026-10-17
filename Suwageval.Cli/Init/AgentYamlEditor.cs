using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Init;

/// <summary>One top-level field of <c>agent.yaml</c> that <c>init</c> sets, and what it was (null: not in the file).</summary>
internal sealed record FieldChange(string Field, string? Old, string New);

/// <summary>
/// Sets top-level fields of <c>agent.yaml</c> on the lines of the text, never through a YAML round trip, which would lose the comments and the
/// formatting of everything else: a field that is there gets its line replaced (an end-of-line comment stays), one that is not is appended.
/// </summary>
internal static partial class AgentYamlEditor
{
    private const string Header =
        "# The agent under test. Its version (prompt, model, tools, events) is read from the running agent at\n"
        + "# GET {endpointUrl}/suwageval/contract, not written here.\n"
        + "# Format: https://github.com/Manomenu/suwalski-agent-eval/blob/master/docs/scenario-format.md\n";

    /// <summary>The scalar top-level fields the file has now, or null with the file's problems when it is not a YAML mapping.</summary>
    public static Dictionary<string, string>? Current(string file, string text, List<Problem> problems)
    {
        var source = YamlSource.Read(file, text, problems);
        if (source is null)
        {
            return null;
        }
        if (source.Root is not JsonObject root)
        {
            problems.Add(source.Problem("", "The file is a mapping with 'name' and 'endpointUrl'."));
            return null;
        }
        return root.Where(pair => pair.Value is JsonValue).ToDictionary(pair => pair.Key, pair => pair.Value!.ToString());
    }

    public static string NewFile(IReadOnlyList<(string Field, string Value)> fields) =>
        Header + string.Concat(fields.Select(field => $"{field.Field}: {Scalar(field.Value)}\n"));

    /// <summary>
    /// The text with <paramref name="fields"/> set and what changed (nothing: the text comes back as it was), or null with the file's problems
    /// when it is not a YAML mapping.
    /// </summary>
    public static (string Text, IReadOnlyList<FieldChange> Changes)? Edit(
        string file,
        string text,
        IReadOnlyList<(string Field, string Value)> fields,
        List<Problem> problems
    )
    {
        if (Current(file, text, problems) is not { } present)
        {
            return null;
        }

        var changes = new List<FieldChange>();
        var edited = text;
        foreach (var (field, value) in fields)
        {
            var old = present.GetValueOrDefault(field);
            if (old == value)
            {
                continue;
            }
            changes.Add(new FieldChange(field, old, value));
            var line = FieldLine(field);
            edited = line.IsMatch(edited)
                ? line.Replace(edited, match => $"{field}: {Scalar(value)}{match.Groups["comment"].Value}", 1)
                : Append(edited, $"{field}: {Scalar(value)}");
        }
        return (edited, changes);
    }

    private static string Append(string text, string line)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return (text.Length == 0 || text.EndsWith('\n') ? text : text + newline) + line + newline;
    }

    /// <summary>A text that reads back as itself: plain when YAML would take it for text anyway, else in double quotes.</summary>
    private static string Scalar(string value)
    {
        var plain =
            PlainText().IsMatch(value)
            && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            && !bool.TryParse(value, out _)
            && !value.Equals("null", StringComparison.OrdinalIgnoreCase);
        return plain
            ? value
            : $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static Regex FieldLine(string field) =>
        new(
            $"""^{Regex.Escape(field)}:[ \t]*(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\r\n]*?)(?<comment>[ \t]+#[^\r\n]*)?(?=\r?$)""",
            RegexOptions.Multiline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)
        );

    [GeneratedRegex("""^[A-Za-z0-9][^\s#"'\\:]*(:[^\s#"'\\:]+)*$""")]
    private static partial Regex PlainText();
}
