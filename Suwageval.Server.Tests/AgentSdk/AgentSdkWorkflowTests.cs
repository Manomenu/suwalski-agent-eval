using System.Text.RegularExpressions;

namespace Suwageval.Server.Tests.AgentSdk;

/// <summary>
/// The release workflow picks the SDK's tests by class name, and a filter that matches nothing is silent while another still matches:
/// a renamed class would quietly leave the release's proof.
/// </summary>
public sealed partial class AgentSdkWorkflowTests
{
    [Fact]
    public void Every_test_class_the_release_workflow_names_exists()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", "agent-sdk.yml"));
        var named = FilterClass().Matches(workflow).Select(match => match.Groups[1].Value).ToList();
        var classes = typeof(AgentSdkWorkflowTests).Assembly.GetTypes().Select(type => type.Name).ToHashSet();

        Assert.NotEmpty(named);
        var missing = named.Where(name => !classes.Contains(name)).ToList();
        Assert.True(
            missing.Count == 0,
            $"fix --filter-class in .github/workflows/agent-sdk.yml, no such test class: {string.Join(", ", missing)}"
        );
    }

    [GeneratedRegex("""--filter-class "\*(\w+)"$""", RegexOptions.Multiline)]
    private static partial Regex FilterClass();

    private static string RepositoryRoot()
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
}
