using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Suwageval.Gate.Code;

public sealed class EscapeHatchTests
{
    [Fact]
    public void Nothing_is_typed_dynamic()
    {
        var found = SourceFile
            .All.SelectMany(file =>
                file.Root.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(name => name.Identifier.Text == "dynamic")
                    .Select(name => $"{file.Path}:{file.LineOf(name) + 1}")
            )
            .ToList();

        Assert.True(
            found.Count == 0,
            $"give it a real type (a record, a generic): dynamic switches the compiler off: {string.Join(", ", found)}"
        );
    }

    [Fact]
    public void A_null_forgiving_operator_says_why_on_its_line_or_the_line_above()
    {
        var found = SourceFile
            .All.Where(file => !IsTest(file))
            .SelectMany(file =>
                file.Root.DescendantNodes()
                    .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                    .Select(node => (file, line: file.LineOf(node)))
                    .Where(item =>
                        !item.file.LineText(item.line).Contains("// Why:", StringComparison.Ordinal)
                        && !item.file.LineText(item.line - 1).Contains("// Why:", StringComparison.Ordinal)
                    )
                    .Select(item => $"{item.file.Path}:{item.line + 1}")
            )
            .ToList();

        Assert.True(
            found.Count == 0,
            $"remove the `!`: check for null or fix the type; if the code really knows better, write `// Why: <reason>` on that line or the line above: {string.Join(", ", found)}"
        );
    }

    // Why: in a test the `!` on a value it just asserted fails the test when it is null, which is what the test is for
    private static bool IsTest(SourceFile file) => file.Path.Split('/')[0].EndsWith(".Tests", StringComparison.Ordinal);
}
