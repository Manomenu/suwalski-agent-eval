using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Suwageval.Gate.Code;

public sealed class NamingTests
{
    private static readonly string[] SharedSimpleNames = [];

    [Fact]
    public void No_two_types_share_a_simple_name()
    {
        var clashes = SolutionTypes
            .All.GroupBy(SolutionTypes.SimpleName)
            .Where(group => group.Count() > 1 && !SharedSimpleNames.Contains(group.Key))
            .Select(group => $"{group.Key} ({string.Join(", ", group.Select(type => type.FullName).Order())})")
            .Order()
            .ToList();

        Assert.True(
            clashes.Count == 0,
            $"rename all but one so that grep for the class name finds exactly one definition: {string.Join("; ", clashes)}"
        );
    }

    [Fact]
    public void Every_source_file_holds_one_top_level_type_named_like_the_file()
    {
        var problems = new List<string>();
        foreach (var file in SourceFile.All)
        {
            var names = file
                .Root.DescendantNodes()
                .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax && node.Parent is not TypeDeclarationSyntax)
                .Select(node =>
                    node is BaseTypeDeclarationSyntax type ? type.Identifier.Text : ((DelegateDeclarationSyntax)node).Identifier.Text
                )
                .ToList();
            var stem = Path.GetFileNameWithoutExtension(file.Path);
            if (names.Count > 1)
            {
                problems.Add($"{file.Path} holds {names.Count} top-level types ({string.Join(", ", names)}): one file per type");
            }
            else if (names.Count == 1 && names[0] != stem)
            {
                problems.Add($"{file.Path} holds {names[0]}: rename the file to {names[0]}.cs, or the type to {stem}");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
