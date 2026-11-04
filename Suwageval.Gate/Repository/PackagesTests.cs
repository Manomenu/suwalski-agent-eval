using System.Xml.Linq;

namespace Suwageval.Gate.Repository;

public sealed class PackagesTests
{
    private static readonly Dictionary<string, string> Banned = new()
    {
        ["MediatR"] = "call the handler explicitly from the endpoint: grep must find the call",
        ["AutoMapper"] = "write the mapping by hand (a ToNote() method): grep must find it",
        ["Mapster"] = "write the mapping by hand (a ToNote() method): grep must find it",
        ["Scrutor"] = "register services explicitly in ServerApp, no assembly scanning",
        ["Moq"] = "test against the real thing (a real PostgreSQL) or a recorded answer",
        ["NSubstitute"] = "test against the real thing (a real PostgreSQL) or a recorded answer",
        ["FakeItEasy"] = "test against the real thing (a real PostgreSQL) or a recorded answer",
        ["FluentAssertions"] = "use xUnit's Assert",
        ["Newtonsoft.Json"] = "use System.Text.Json",
        ["Microsoft.EntityFrameworkCore"] = "use Dapper with hand-written SQL",
    };

    private static IEnumerable<string> ProjectFiles() =>
        RepositoryFiles.Paths().Where(path => path.EndsWith(".csproj", StringComparison.Ordinal) || path == "Directory.Build.props");

    private static IEnumerable<string> Includes(string path, string element) =>
        XDocument
            .Load(Path.Combine(RepositoryFiles.Root, path))
            .Descendants(element)
            .Select(item => (string?)item.Attribute("Include") ?? "");

    private static IEnumerable<string> Referenced() => ProjectFiles().SelectMany(path => Includes(path, "PackageReference"));

    private static IEnumerable<string> Versioned() => Includes("Directory.Packages.props", "PackageVersion");

    [Fact]
    public void No_banned_package_is_versioned_or_referenced()
    {
        var found = Referenced()
            .Concat(Versioned())
            .Distinct()
            .SelectMany(package =>
                Banned
                    .Where(entry => package.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => $"{package}: {entry.Value}")
            )
            .Order()
            .ToList();

        Assert.True(found.Count == 0, $"remove the package and use the alternative: {string.Join("; ", found)}");
    }

    [Fact]
    public void Every_versioned_package_is_referenced_by_a_project()
    {
        var referenced = Referenced().ToHashSet();

        var unused = Versioned().Where(package => !referenced.Contains(package)).Order().ToList();

        Assert.True(
            unused.Count == 0,
            $"remove it from Directory.Packages.props, a package arrives with the first code that uses it: {string.Join(", ", unused)}"
        );
    }
}
