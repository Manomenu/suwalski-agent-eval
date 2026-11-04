using System.Text.Json;
using System.Text.RegularExpressions;

namespace Suwageval.Gate.Repository;

public sealed partial class AppSettingsTests
{
    private static readonly string[] Framework = ["Logging", "AllowedHosts", "ConnectionStrings"];

    [GeneratedRegex(@"(?:BindConfiguration\(\s*|const string Section = )""([^"":]+)")]
    private static partial Regex Binding();

    [Fact]
    public void Every_section_of_appsettings_is_bound_by_an_options_class()
    {
        var bound = SourceFile
            .All.SelectMany(file => Binding().Matches(file.Root.ToFullString()).Select(match => match.Groups[1].Value))
            .ToHashSet();
        var sections = RepositoryFiles
            .Paths()
            .Where(path =>
                Path.GetFileName(path).StartsWith("appsettings", StringComparison.Ordinal)
                && path.EndsWith(".json", StringComparison.Ordinal)
            )
            .SelectMany(path =>
                JsonDocument.Parse(RepositoryFiles.Read(path)).RootElement.EnumerateObject().Select(property => $"{property.Name} ({path})")
            )
            .Where(section => !Framework.Contains(section.Split(' ')[0]) && !bound.Contains(section.Split(' ')[0]))
            .Order()
            .ToList();

        Assert.True(
            sections.Count == 0,
            $"remove the setting or add the code that reads it (AddOptions<T>().BindConfiguration(\"<Section>\")): {string.Join(", ", sections)}"
        );
    }
}
