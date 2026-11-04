using System.Text.Json;
using System.Text.RegularExpressions;

namespace Suwageval.Gate.Repository;

public sealed partial class VsCodeSettingsTests
{
    private static readonly JsonDocumentOptions Jsonc = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly string[] ToolPaths =
    [
        "prettier.prettierPath",
        "prettier.configPath",
        "prettier.ignorePath",
        "prettier.requireConfig",
    ];

    [GeneratedRegex(
        @"^(editor\.(tabSize|insertSpaces|detectIndentation|rulers|wordWrap|wordWrapColumn|trimAutoWhitespace|renderWhitespace)|files\.(eol|insertFinalNewline|trimFinalNewlines|trimTrailingWhitespace|encoding|autoGuessEncoding)|csharpier\..*|prettier\..*)$"
    )]
    private static partial Regex StyleKey();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*\.[a-z0-9][a-z0-9-]*$")]
    private static partial Regex ExtensionId();

    private static bool IsStyle(string key) => StyleKey().IsMatch(key) && !ToolPaths.Contains(key);

    private static IEnumerable<string> Keys(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            yield return property.Name;
            if (property.Name.StartsWith('[') && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var inner in Keys(property.Value))
                {
                    yield return inner;
                }
            }
        }
    }

    [Fact]
    public void Settings_json_holds_glue_and_no_style_rules()
    {
        var settings = JsonDocument.Parse(RepositoryFiles.Read(".vscode/settings.json"), Jsonc).RootElement;

        var style = Keys(settings).Where(IsStyle).Order().ToList();

        Assert.True(
            style.Count == 0,
            $"put it in .editorconfig / .prettierrc.json / .csharpierrc.json so every editor uses it: {string.Join(", ", style)}"
        );
    }

    [Theory]
    [InlineData("editor.tabSize", true)]
    [InlineData("files.eol", true)]
    [InlineData("prettier.printWidth", true)]
    [InlineData("prettier.prettierPath", false)]
    [InlineData("editor.formatOnSave", false)]
    [InlineData("eslint.workingDirectories", false)]
    public void The_style_rule_knows_a_style_key(string key, bool expected) => Assert.Equal(expected, IsStyle(key));

    [Fact]
    public void Every_recommended_extension_id_is_well_formed_and_listed_once()
    {
        var ids = JsonDocument
            .Parse(RepositoryFiles.Read(".vscode/extensions.json"), Jsonc)
            .RootElement.GetProperty("recommendations")
            .EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .ToList();

        var bad = ids.Where(id => !ExtensionId().IsMatch(id))
            .Concat(ids.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key))
            .ToList();

        Assert.True(
            bad.Count == 0,
            $"write each id as publisher.name in lower case, once (the Marketplace id from the extension page): {string.Join(", ", bad)}"
        );
    }
}
