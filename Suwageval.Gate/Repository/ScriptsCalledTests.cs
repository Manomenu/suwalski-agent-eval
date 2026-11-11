namespace Suwageval.Gate.Repository;

// Why: a script nobody calls is code nobody runs, so it rots unseen; a recipe a person runs is the call that keeps it alive.
public sealed class ScriptsCalledTests
{
    private static bool IsScript(string path) =>
        path.EndsWith(".sh", StringComparison.Ordinal)
        && (path.StartsWith("scripts/", StringComparison.Ordinal) || path.StartsWith("Suwageval.Gate/scripts/", StringComparison.Ordinal));

    // Why: the places that run something; a doc naming a script or a test running it to check it calls nothing for real.
    private static bool Calls(string path) =>
        path.EndsWith(".sh", StringComparison.Ordinal)
        || path.EndsWith(".just", StringComparison.Ordinal)
        || path is "justfile" or ".claude/settings.json" or ".vscode/tasks.json"
        || path.StartsWith(".githooks/", StringComparison.Ordinal)
        || path.StartsWith(".github/workflows/", StringComparison.Ordinal);

    [Fact]
    public void Every_script_is_called_by_a_recipe_another_script_or_a_hook()
    {
        var paths = RepositoryFiles.Paths().Where(path => File.Exists(Path.Combine(RepositoryFiles.Root, path))).ToArray();
        var callers = paths.Where(Calls).Select(path => (path, text: RepositoryFiles.Read(path))).ToList();

        var uncalled = paths
            .Where(IsScript)
            .Where(script =>
                !callers.Any(caller => caller.path != script && caller.text.Contains(Path.GetFileName(script), StringComparison.Ordinal))
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            uncalled.Count == 0,
            $"nothing calls it: delete it, or call it from the recipe, script or hook that needs it: {string.Join(", ", uncalled)}"
        );
    }
}
