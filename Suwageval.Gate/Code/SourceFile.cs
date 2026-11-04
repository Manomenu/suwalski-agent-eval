using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Suwageval.Gate.Code;

internal sealed class SourceFile(string path, string text)
{
    private static readonly Lazy<IReadOnlyList<SourceFile>> Solution = new(() =>
        [.. RepositoryFiles.CSharpSources().Select(path => new SourceFile(path, RepositoryFiles.Read(path)))]
    );

    public string Path { get; } = path;

    public SyntaxTree Tree { get; } = CSharpSyntaxTree.ParseText(text);

    public SyntaxNode Root => Tree.GetRoot();

    public static IReadOnlyList<SourceFile> All => Solution.Value;

    public int LineOf(SyntaxNode node) => Tree.GetLineSpan(node.Span).StartLinePosition.Line;

    public string LineText(int zeroBasedLine) => zeroBasedLine < 0 ? "" : Tree.GetText().Lines[zeroBasedLine].ToString();
}
