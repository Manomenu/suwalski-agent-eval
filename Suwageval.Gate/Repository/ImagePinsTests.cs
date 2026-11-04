using System.Text.RegularExpressions;

namespace Suwageval.Gate.Repository;

public sealed partial class ImagePinsTests
{
    [GeneratedRegex(@"^\s*(?:FROM\s+(?:--platform=\S+\s+)?|image:\s*)(\S+)", RegexOptions.Multiline)]
    private static partial Regex ImageReference();

    [Fact]
    public void Every_image_is_pinned_to_a_version_never_latest()
    {
        var unpinned = RepositoryFiles
            .Paths()
            .Where(path => Path.GetFileName(path) is "Dockerfile" or "compose.yaml")
            .SelectMany(path =>
                ImageReference().Matches(RepositoryFiles.Read(path)).Select(match => (path, image: match.Groups[1].Value.Trim('"')))
            )
            .Where(item => !item.image.Contains(':', StringComparison.Ordinal) || item.image.EndsWith(":latest", StringComparison.Ordinal))
            .Select(item => $"{item.path}: {item.image}")
            .Order()
            .ToList();

        Assert.True(
            unpinned.Count == 0,
            $"pin it to a version (name:1.2.3), a moving tag makes builds irreproducible: {string.Join(", ", unpinned)}"
        );
    }
}
