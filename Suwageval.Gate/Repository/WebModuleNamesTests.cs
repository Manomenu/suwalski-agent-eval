namespace Suwageval.Gate.Repository;

public sealed class WebModuleNamesTests
{
    [Fact]
    public void No_two_web_modules_in_one_folder_differ_only_in_letter_case_of_their_name()
    {
        var clashes = ArchitectureTests
            .Repository.Paths.Where(path => path.StartsWith("Suwageval.Web/src/", StringComparison.Ordinal))
            .Where(path => path.EndsWith(".ts", StringComparison.Ordinal) || path.EndsWith(".tsx", StringComparison.Ordinal))
            .Select(path => (Folder: path[..path.LastIndexOf('/')], File: path[(path.LastIndexOf('/') + 1)..]))
            .Select(item => (item.Folder, Stem: item.File.Split('.')[0]))
            .Distinct()
            .GroupBy(item => (item.Folder, Stem: item.Stem.ToUpperInvariant()))
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" vs ", group.Select(item => $"{item.Folder}/{item.Stem}").Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            clashes.Count == 0,
            $"web files whose names differ only in letter case, imports would resolve to the wrong one: {string.Join("; ", clashes)}"
        );
    }
}
