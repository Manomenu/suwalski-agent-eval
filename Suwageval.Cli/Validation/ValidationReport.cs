using System.Globalization;
using System.Text.Json;

namespace Suwageval.Cli.Validation;

/// <summary>A result written for a person (<c>file:line:col: path: message</c>) or for a program (<c>--json</c>).</summary>
internal static class ValidationReport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Printed for a program, not embedded in HTML: quotes stay quotes instead of \u0027.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Text(ValidationResult result)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        foreach (var problem in result.Problems)
        {
            var position = problem.Line is { } line ? $":{line}:{problem.Column}" : "";
            var path = problem.Path.Length == 0 ? "" : $" {problem.Path}:";
            writer.WriteLine($"{problem.File}{position}:{path} {problem.Message}");
        }
        if (result.Notice is not null)
        {
            writer.WriteLine(result.Notice);
        }
        writer.WriteLine(
            result.IsValid
                ? $"valid ({result.Scenarios} scenarios, {result.Checks} checks)"
                : $"{result.Problems.Count} problem{(result.Problems.Count == 1 ? "" : "s")}"
        );
        return writer.ToString();
    }

    public static string Json(ValidationResult result) =>
        JsonSerializer.Serialize(
            new JsonReport(
                result.IsValid,
                result.NamesChecked,
                result.Notice,
                [.. result.Problems.Select(p => new JsonProblem(p.File, p.Path, p.Message))]
            ),
            JsonOptions
        );

    private sealed record JsonReport(bool Valid, bool NamesChecked, string? Notice, IReadOnlyList<JsonProblem> Problems);

    private sealed record JsonProblem(string File, string Path, string Message);
}
