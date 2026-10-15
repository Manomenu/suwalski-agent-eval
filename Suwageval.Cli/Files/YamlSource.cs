using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Suwageval.Cli.Files;

/// <summary>
/// A YAML file read into a JSON tree (what the contracts are deserialized from) that remembers where in the file each
/// value came from, so a problem found later is reported with its line and column.
/// </summary>
/// <remarks>
/// Paths look like <c>conversation[0].text</c>. A path in the JSON tree may differ from the one in the file where a
/// shorthand was expanded; <see cref="AddAlias"/> says which file path it stands for.
/// </remarks>
internal sealed partial class YamlSource
{
    private readonly Dictionary<string, (long Line, long Column)> marks = [];
    private readonly Dictionary<string, string> aliases = [];
    private readonly List<Problem> problems;

    private YamlSource(string file, List<Problem> problems)
    {
        File = file;
        this.problems = problems;
    }

    public string File { get; }

    public JsonNode? Root { get; private set; }

    /// <summary>Reads the text; a file that is not YAML, is empty or holds several documents is a problem and returns null.</summary>
    public static YamlSource? Read(string file, string text, List<Problem> problems)
    {
        var source = new YamlSource(file, problems);
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException e)
        {
            var message = LocationPrefix().Replace(e.Message, "");
            problems.Add(new Problem(file, "", message, e.Start.Line, e.Start.Column));
            return null;
        }
        if (stream.Documents.Count == 0)
        {
            problems.Add(new Problem(file, "", "The file is empty.", null, null));
            return null;
        }
        if (stream.Documents.Count > 1)
        {
            problems.Add(new Problem(file, "", "A file holds one YAML document; remove the extra '---' sections.", null, null));
            return null;
        }
        source.Root = source.Convert(stream.Documents[0].RootNode, "");
        return source;
    }

    /// <summary>Tells that the value at <paramref name="expanded"/> in the tree is what the file has at <paramref name="original"/>.</summary>
    public void AddAlias(string expanded, string original) => aliases[expanded] = original;

    public Problem Problem(string path, string message)
    {
        var shown = Translate(path);
        var parent = shown;
        while (true)
        {
            if (marks.TryGetValue(parent, out var mark))
            {
                return new Problem(File, shown, message, mark.Line, mark.Column);
            }
            if (parent.Length == 0)
            {
                return new Problem(File, shown, message, null, null);
            }
            var cut = parent.LastIndexOfAny(['.', '[']);
            parent = cut < 0 ? "" : parent[..cut];
        }
    }

    public static string Child(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    private string Translate(string path)
    {
        string? best = null;
        foreach (var expanded in aliases.Keys)
        {
            var matches =
                path == expanded
                || path.StartsWith(expanded + ".", StringComparison.Ordinal)
                || path.StartsWith(expanded + "[", StringComparison.Ordinal);
            if (matches && (best is null || expanded.Length > best.Length))
            {
                best = expanded;
            }
        }
        return best is null ? path : aliases[best] + path[best.Length..];
    }

    private JsonNode? Convert(YamlNode node, string path)
    {
        marks[path] = (node.Start.Line, node.Start.Column);
        switch (node)
        {
            case YamlMappingNode mapping:
                var obj = new JsonObject();
                foreach (var (key, value) in mapping.Children)
                {
                    if (key is not YamlScalarNode { Value: { } name })
                    {
                        problems.Add(new Problem(File, path, "A key must be plain text.", key.Start.Line, key.Start.Column));
                        continue;
                    }
                    var childPath = Child(path, name);
                    if (obj.ContainsKey(name))
                    {
                        problems.Add(new Problem(File, childPath, $"Duplicate key '{name}'.", key.Start.Line, key.Start.Column));
                        continue;
                    }
                    obj[name] = Convert(value, childPath);
                    marks[childPath] = (key.Start.Line, key.Start.Column);
                }
                return obj;
            case YamlSequenceNode sequence:
                var array = new JsonArray();
                var index = 0;
                foreach (var item in sequence.Children)
                {
                    array.Add(Convert(item, $"{path}[{index}]"));
                    index++;
                }
                return array;
            case YamlScalarNode scalar:
                return Scalar(scalar);
            default:
                problems.Add(new Problem(File, path, "Unsupported YAML construct.", node.Start.Line, node.Start.Column));
                return null;
        }
    }

    /// <summary>Quoted text stays text; a plain scalar is read as YAML's core schema does: null, boolean, number, else text.</summary>
    private static JsonValue? Scalar(YamlScalarNode scalar)
    {
        var text = scalar.Value ?? "";
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(text);
        }
        if (text is "" or "~" or "null" or "Null" or "NULL")
        {
            return null;
        }
        if (text is "true" or "True" or "TRUE")
        {
            return JsonValue.Create(true);
        }
        if (text is "false" or "False" or "FALSE")
        {
            return JsonValue.Create(false);
        }
        if (IntegerPattern().IsMatch(text) && long.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var integer))
        {
            return JsonValue.Create(integer);
        }
        if (
            FloatPattern().IsMatch(text)
            && double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number)
        )
        {
            return JsonValue.Create(number);
        }
        return JsonValue.Create(text);
    }

    [GeneratedRegex(@"^\(.*?\) - \(.*?\): ")]
    private static partial Regex LocationPrefix();

    [GeneratedRegex(@"^[-+]?[0-9]+$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^[-+]?([0-9]+\.[0-9]*|\.[0-9]+|[0-9]+)([eE][-+]?[0-9]+)?$")]
    private static partial Regex FloatPattern();
}
