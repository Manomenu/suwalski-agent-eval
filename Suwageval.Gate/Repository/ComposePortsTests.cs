using System.Text.RegularExpressions;

namespace Suwageval.Gate.Repository;

// Why: the server has no login, and a port published on every interface puts what runs here on the laptop's network.
public sealed partial class ComposePortsTests
{
    [GeneratedRegex(@"^(\s*)ports:\s*$")]
    private static partial Regex PortsKey();

    [GeneratedRegex(@"^\s*-\s*[""']?([^""'\s]+)[""']?\s*$")]
    private static partial Regex ListItem();

    internal static IEnumerable<(int Line, string Port)> PublishedPorts(string compose)
    {
        var lines = compose.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var key = PortsKey().Match(lines[index]);
            if (!key.Success)
            {
                continue;
            }
            for (var item = index + 1; item < lines.Length; item++)
            {
                var line = lines[item];
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }
                if (line.Length - line.TrimStart().Length <= key.Groups[1].Length || ListItem().Match(line) is not { Success: true } entry)
                {
                    break;
                }
                yield return (item + 1, entry.Groups[1].Value);
            }
        }
    }

    [Fact]
    public void Every_compose_file_publishes_its_ports_on_the_loopback_only()
    {
        var open = RepositoryFiles
            .Paths()
            .Where(path => Path.GetFileName(path) is "compose.yaml" or "compose.yml" or "docker-compose.yaml" or "docker-compose.yml")
            .Where(path => File.Exists(Path.Combine(RepositoryFiles.Root, path)))
            .SelectMany(path =>
                PublishedPorts(RepositoryFiles.Read(path))
                    .Where(port => !port.Port.StartsWith("127.0.0.1:", StringComparison.Ordinal))
                    .Select(port => $"{path}:{port.Line} {port.Port}")
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            open.Count == 0,
            $"publish it as \"127.0.0.1:<host port>:<container port>\": a port on every interface puts what runs here on the network: {string.Join(", ", open)}"
        );
    }

    [Theory]
    [InlineData("    ports:\n      - \"127.0.0.1:6320:6320\"\n    volumes:\n      - x:/y", "127.0.0.1:6320:6320")]
    [InlineData("    ports:\n      - 8080:80\n", "8080:80")]
    [InlineData("    ports:\n      # a note\n      - '5432:5432'\n  db:\n", "5432:5432")]
    public void The_reader_finds_each_published_port(string compose, string expected)
    {
        ArgumentNullException.ThrowIfNull(compose);
        Assert.Equal([expected], PublishedPorts(compose).Select(port => port.Port));
    }
}
