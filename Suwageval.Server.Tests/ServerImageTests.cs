using System.Xml.Linq;

namespace Suwageval.Server.Tests;

/// <summary>
/// The server's image builds from the projects it references: each of them is copied into the build stage (project file
/// and lockfile for the restore, then its sources) and none is kept out of the build context. The gate does not build the
/// image, so without this a new reference fails only in CI's image job. A repo-content test: it reads the Dockerfile.
/// </summary>
public sealed class ServerImageTests
{
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

    /// <summary>The server and every project it references, directly or through another.</summary>
    private static List<string> ServerProjects()
    {
        var found = new List<string>();
        var queue = new Queue<string>(["Suwageval.Server"]);
        while (queue.TryDequeue(out var project))
        {
            if (found.Contains(project))
            {
                continue;
            }
            found.Add(project);
            foreach (
                var reference in XDocument
                    .Load(Path.Combine(RepoRoot, project, project + ".csproj"))
                    .Descendants("ProjectReference")
                    .Select(item => Path.GetFileNameWithoutExtension(((string)item.Attribute("Include")!).Replace('\\', '/')))
            )
            {
                queue.Enqueue(reference);
            }
        }
        return found;
    }

    [Fact]
    public void Every_project_the_server_builds_from_is_copied_into_its_image()
    {
        var copies = File.ReadAllLines(Path.Combine(RepoRoot, "Suwageval.Server", "Dockerfile"))
            .Where(line => line.StartsWith("COPY ", StringComparison.Ordinal))
            .ToList();
        var ignored = File.ReadAllLines(Path.Combine(RepoRoot, ".dockerignore")).Select(line => line.Trim()).ToHashSet();

        foreach (var project in ServerProjects())
        {
            Assert.True(
                copies.Any(line => line.Contains($"{project}/{project}.csproj {project}/packages.lock.json", StringComparison.Ordinal)),
                $"Suwageval.Server/Dockerfile: copy {project}/{project}.csproj and its packages.lock.json before the restore"
            );
            Assert.True(copies.Contains($"COPY {project}/ {project}/"), $"Suwageval.Server/Dockerfile: copy {project}/ before the publish");
            Assert.False(ignored.Contains($"{project}/"), $".dockerignore keeps {project}/ out of the build context");
        }
    }
}
