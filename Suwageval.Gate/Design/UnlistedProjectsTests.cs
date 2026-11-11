using System.Text.RegularExpressions;

namespace Suwageval.Gate.Design;

// Why: a guard over a list passes in silence for whatever is left off the list, so what the list leaves out has to fail by itself
public sealed partial class UnlistedProjectsTests
{
    // Why: their namespaces mirror the server's areas as plain records, with no layer above another to keep apart
    private static readonly string[] WithoutLayers = ["Suwageval.Contracts", "Suwageval.AgentSdk"];

    // Why: their layers are server and cli in layers.json, guarded by ServerContractsTests and CliContractsTests
    private static readonly string[] Layered = ["Suwageval.Server", "Suwageval.Cli"];

    [GeneratedRegex(@"<AssemblyName>([^<]+)</AssemblyName>")]
    private static partial Regex AssemblyNameProperty();

    private static IEnumerable<string> ProjectsWithProductionCode() =>
        LayersFile.Projects().Keys.Where(project => !project.EndsWith(".Tests", StringComparison.Ordinal));

    private static string AssemblyOf(string project) =>
        AssemblyNameProperty().Match(RepositoryFiles.Read($"{project}/{project}.csproj")) is { Success: true } name
            ? name.Groups[1].Value
            : project;

    [Fact]
    public void Every_project_is_read_by_the_guards_over_types()
    {
        var read = SolutionTypes.Assemblies.Select(assembly => assembly.GetName().Name).ToHashSet();
        var unread = ProjectsWithProductionCode().Where(project => !read.Contains(AssemblyOf(project))).Order().ToList();

        Assert.True(
            unread.Count == 0,
            $"add the project's assembly to Assemblies in Suwageval.Gate/Design/SolutionTypes.cs, or the naming, interface and unused-type guards never see it: {string.Join(", ", unread)}"
        );
    }

    [Fact]
    public void Every_project_has_layers_or_says_why_not()
    {
        var gate = typeof(UnlistedProjectsTests).Assembly.GetName().Name;
        var unlisted = ProjectsWithProductionCode()
            .Where(project => project != gate && !Layered.Contains(project) && !WithoutLayers.Contains(project))
            .Order()
            .ToList();

        Assert.True(
            unlisted.Count == 0,
            $"give the project its layers in {LayersFile.Path} and a contracts test like ServerContractsTests, then add it to Layered here; or, when it has nothing to layer, add it to WithoutLayers with the reason: {string.Join(", ", unlisted)}"
        );
    }
}
