using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Suwageval.Gate.Code;

public sealed class NoPragmaTests
{
    [Fact]
    public void No_source_file_switches_a_warning_off_with_a_pragma()
    {
        var offenders = SourceFile
            .All.Where(file =>
                file.Root.DescendantNodes(descendIntoTrivia: true)
                    .OfType<PragmaWarningDirectiveTriviaSyntax>()
                    .Any(pragma => pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            )
            .Select(file => file.Path)
            .Order()
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "switch the rule off in .editorconfig with a reason, or put [SuppressMessage(..., Justification = ...)] on the member: "
                + string.Join(", ", offenders)
        );
    }
}
