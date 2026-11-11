namespace Suwageval.Server.Tests.Agents;

public sealed class ContractDocsTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Theory]
    [Trait("criterion", "AC-system-tools-8")]
    [InlineData("docs/scenario-format.md", "| `context.tools` |")]
    [InlineData("docs/scenario-format.md", "a tool of `context.tools` takes no `by`")]
    [InlineData("claude-plugin/skills/suwageval/SKILL.md", "goes to\n  `context.tools`, never under an LLM agent")]
    [InlineData("Suwageval.AgentSdk/README.md", "`ContextDefinition.Tools`")]
    public void The_docs_name_the_tools_of_the_systems_code(string path, string text) =>
        Assert.Contains(text, File.ReadAllText(Path.Combine(RepoRoot, path)).ReplaceLineEndings("\n"), StringComparison.Ordinal);

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
}
