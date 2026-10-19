using System.Xml.Linq;

namespace Suwageval.Server.Tests;

/// <summary>
/// Which project may reference which, and who may see whose internals (AGENTS.md, section 4). The projects form a graph
/// that only points one way: the CLI and the server know the contracts and nothing of each other; only the server's tests
/// know the CLI, for the end-to-end tests. A repo-content test: it reads the solution and the project files.
/// </summary>
public sealed class ProjectReferencesTests
{
    private sealed record Allowed(string[] References, string[] InternalsVisibleTo);

    private static readonly Dictionary<string, Allowed> Table = new()
    {
        ["Suwageval.AgentSdk"] = new([], []),
        ["Suwageval.Contracts"] = new(["Suwageval.AgentSdk"], []),
        ["Suwageval.Server"] = new(["Suwageval.Contracts"], ["Suwageval.Server.Tests"]),
        ["Suwageval.Cli"] = new(["Suwageval.Contracts"], ["Suwageval.Cli.Tests", "Suwageval.Server.Tests"]),
        ["Suwageval.Cli.Tests"] = new(["Suwageval.Cli"], []),
        ["Suwageval.Server.Tests"] = new(["Suwageval.Server", "Suwageval.Cli"], []),
    };

    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Suwageval.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("Suwageval.slnx not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Every project of the solution, by name, with its project file.</summary>
    private static Dictionary<string, XDocument> Projects() =>
        XDocument
            .Load(Path.Combine(RepoRoot, "Suwageval.slnx"))
            .Descendants("Project")
            .Select(project => Path.Combine(RepoRoot, (string)project.Attribute("Path")!))
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), XDocument.Load);

    public static TheoryData<string> ProjectNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in Table.Keys)
        {
            data.Add(name);
        }
        return data;
    }

    [Fact]
    public void Every_project_of_the_solution_is_in_the_table()
    {
        var unknown = Projects().Keys.Except(Table.Keys).Order().ToList();
        var missing = Table.Keys.Except(Projects().Keys).Order().ToList();

        Assert.True(unknown.Count == 0, $"add to Table in ProjectReferencesTests.cs: {string.Join(", ", unknown)}");
        Assert.True(missing.Count == 0, $"in Table but not in Suwageval.slnx: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(ProjectNames))]
    public void A_project_references_only_the_projects_the_table_allows(string project)
    {
        var references = Projects()[project]
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(((string)reference.Attribute("Include")!).Replace('\\', '/')))
            .Order()
            .ToList();

        var forbidden = references.Except(Table[project].References).ToList();
        Assert.True(forbidden.Count == 0, $"{project} must not reference {string.Join(", ", forbidden)} (AGENTS.md, section 4)");
        var missing = Table[project].References.Except(references).ToList();
        Assert.True(missing.Count == 0, $"{project} no longer references {string.Join(", ", missing)}: update Table");
    }

    [Theory]
    [MemberData(nameof(ProjectNames))]
    public void A_project_shows_its_internals_only_to_the_projects_the_table_allows(string project)
    {
        var visibleTo = Projects()[project]
            .Descendants("InternalsVisibleTo")
            .Select(item => (string)item.Attribute("Include")!)
            .Order()
            .ToList();

        var forbidden = visibleTo.Except(Table[project].InternalsVisibleTo).ToList();
        Assert.True(
            forbidden.Count == 0,
            $"{project} must not show its internals to {string.Join(", ", forbidden)} (AGENTS.md, section 4)"
        );
        var missing = Table[project].InternalsVisibleTo.Except(visibleTo).ToList();
        Assert.True(missing.Count == 0, $"{project} no longer shows its internals to {string.Join(", ", missing)}: update Table");
    }
}
