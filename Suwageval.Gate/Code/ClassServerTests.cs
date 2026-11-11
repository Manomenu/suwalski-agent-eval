using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Suwageval.Gate.Code;

// Why: xunit makes a test class anew for every test, so a server started in a field starts once per test; one per class took the server's tests from 18 s to 12 s (2026-11-11)
public sealed class ClassServerTests
{
    private static bool StartsAServer(string initializer) =>
        initializer.Contains("RunTestApp.Create(", StringComparison.Ordinal)
        || initializer.Contains("WebApplicationFactory<Program>(", StringComparison.Ordinal)
        || initializer.Contains("new TestServer(", StringComparison.Ordinal);

    [Fact]
    public void No_test_class_starts_the_server_in_a_field()
    {
        var found = SourceFile
            .All.Where(file => file.Path.StartsWith("Suwageval.Server.Tests/", StringComparison.Ordinal))
            .Where(file => file.Path != "Suwageval.Server.Tests/ClassServer.cs")
            .SelectMany(file =>
                file.Root.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .Where(field =>
                        field.Declaration.Variables.Any(variable => variable.Initializer is { } value && StartsAServer(value.ToString()))
                    )
                    .Select(field => (SyntaxNode)field)
                    .Concat(
                        file.Root.DescendantNodes()
                            .OfType<PropertyDeclarationSyntax>()
                            .Where(property => property.Initializer is { } value && StartsAServer(value.ToString()))
                    )
                    .Select(member => $"{file.Path}:{file.LineOf(member) + 1}")
            )
            .ToList();

        Assert.True(
            found.Count == 0,
            $"take the server as a class fixture: (TestDatabase database, ClassServer server) : IClassFixture<ClassServer>, then private readonly WebApplicationFactory<Program> app = server.App; a test that needs other settings makes its own factory in an await using inside the test: {string.Join(", ", found)}"
        );
    }
}
