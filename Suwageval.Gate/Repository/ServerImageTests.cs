using System.Xml.Linq;

namespace Suwageval.Gate.Repository;

public sealed class ServerImageTests
{
    private static readonly string RepoRoot = RepositoryFiles.Root;

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
