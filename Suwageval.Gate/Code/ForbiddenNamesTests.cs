using System.Text.RegularExpressions;

namespace Suwageval.Gate.Code;

public sealed partial class ForbiddenNamesTests
{
    private const string Fix =
        "name it after the feature or after what it is (Database, Output), not who uses it; one feature = one folder";

    private static readonly string[] Vague =
    [
        "Common",
        "Helper",
        "Helpers",
        "Manager",
        "Misc",
        "Shared",
        "Stuff",
        "Util",
        "Utilities",
        "Utility",
        "Utils",
    ];

    private static readonly string[] LayerByKind =
    [
        "Application",
        "Components",
        "Controllers",
        "Core",
        "Domain",
        "Hooks",
        "DTOs",
        "Infrastructure",
        "Interfaces",
        "Lib",
        "Models",
        "Repositories",
        "Services",
        "Ui",
    ];

    private static readonly string[] ForbiddenSuffixes = ["Repository", "Service", "UnitOfWork"];

    private static readonly string[] Segments = [.. Vague, .. LayerByKind, "Base"];

    [GeneratedRegex(@"[A-Z][a-z0-9]*")]
    private static partial Regex Word();

    public static string? ForbiddenIn(string name, string[] forbidden) =>
        forbidden.FirstOrDefault(entry =>
            string.Equals(entry, name, StringComparison.OrdinalIgnoreCase) || Word().Matches(name).Any(word => word.Value == entry)
        );

    [Theory]
    [InlineData("StringHelpers", "Helpers")]
    [InlineData("Utils", "Utils")]
    [InlineData("OutputManager", "Manager")]
    [InlineData("Dtos", "DTOs")]
    [InlineData("Base", "Base")]
    [InlineData("Database", null)]
    [InlineData("Notes", null)]
    [InlineData("Scoreboard", null)]
    public void A_forbidden_word_is_found_by_its_words_not_by_letters(string name, string? expected) =>
        Assert.Equal(expected, ForbiddenIn(name, Segments));

    [Fact]
    public void No_folder_or_project_is_named_by_a_forbidden_word()
    {
        // Why: the plugin's folder names are Claude Code's own (hooks/, skills/)
        var paths = RepositoryFiles.Paths().Where(path => !path.StartsWith("claude-plugin/", StringComparison.Ordinal)).ToArray();
        var names = paths
            .SelectMany(path => path.Split('/')[..^1])
            .Concat(
                paths
                    .Where(path => path.EndsWith(".csproj", StringComparison.Ordinal))
                    .Select(path => Path.GetFileNameWithoutExtension(path))
            )
            .SelectMany(name => name.Split('.'))
            .Distinct();

        var found = names
            .Select(name => (name, word: ForbiddenIn(name, Segments)))
            .Where(item => item.word is not null)
            .Select(item => $"{item.name} ({item.word})")
            .Order()
            .ToList();

        Assert.True(found.Count == 0, $"{Fix}: {string.Join(", ", found)}");
    }

    [Fact]
    public void No_namespace_segment_is_a_forbidden_word()
    {
        var found = SolutionTypes
            .All.SelectMany(type => (type.Namespace ?? "").Split('.'))
            .Distinct()
            .Select(name => (name, word: ForbiddenIn(name, Segments)))
            .Where(item => item.word is not null)
            .Select(item => $"{item.name} ({item.word})")
            .Order()
            .ToList();

        Assert.True(found.Count == 0, $"{Fix}: {string.Join(", ", found)}");
    }

    [Fact]
    public void No_type_is_named_with_a_vague_word_or_a_forbidden_suffix()
    {
        var found = SolutionTypes
            .All.Select(type => (type, name: SolutionTypes.SimpleName(type)))
            .Select(item =>
                (item.type, item.name, word: ForbiddenIn(item.name, Vague) ?? ForbiddenSuffixes.FirstOrDefault(item.name.EndsWith))
            )
            .Where(item => item.word is not null)
            .Select(item => $"{item.type.FullName} ({item.word})")
            .Order()
            .ToList();

        Assert.True(found.Count == 0, $"{Fix}: {string.Join(", ", found)}");
    }
}
