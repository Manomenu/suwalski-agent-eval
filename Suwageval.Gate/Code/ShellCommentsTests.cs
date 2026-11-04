using System.Text.RegularExpressions;

namespace Suwageval.Gate.Code;

public sealed partial class ShellCommentsTests
{
    private const string Fix =
        "delete it, rename the code so it explains itself, print it on --help when it says how to call the script, or write `# Why: <reason>` on one line.";

    [GeneratedRegex(@"^# Why: \S.*$")]
    private static partial Regex Why();

    [GeneratedRegex(@"^# shellcheck ")]
    private static partial Regex ToolMarker();

    [GeneratedRegex(@"<<-?\s*['""]?(\w+)['""]?")]
    private static partial Regex HereDocument();

    private static IEnumerable<string> Scripts() =>
        RepositoryFiles
            .Paths()
            .Where(path => path.EndsWith(".sh", StringComparison.Ordinal) || path.StartsWith(".githooks/", StringComparison.Ordinal));

    [Fact]
    public void Every_shell_comment_is_a_Why_or_a_tool_marker()
    {
        var problems = Scripts()
            .SelectMany(script =>
                CommentsIn(RepositoryFiles.Read(script))
                    .Where(comment => !Why().IsMatch(comment.Text) && !ToolMarker().IsMatch(comment.Text))
                    .Select(comment => $"{script}:{comment.Line}: {comment.Text}")
            )
            .ToList();

        Assert.True(problems.Count == 0, $"{Fix}{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    [Theory]
    [InlineData("#!/usr/bin/env bash\necho hi", 0)]
    [InlineData("# a comment", 1)]
    [InlineData("echo hi # at the end", 1)]
    [InlineData("echo \"a # b\" 'c # d'", 0)]
    [InlineData("echo ${#list[@]} $#", 0)]
    [InlineData("cat <<'HELP'\n# not a comment\nHELP\n# a comment", 1)]
    [InlineData("a='x\n# inside a string\ny'", 0)]
    public void Comment_signs_are_told_from_comments(string script, int expected) => Assert.Equal(expected, CommentsIn(script ?? "").Count);

    private static List<(int Line, string Text)> CommentsIn(string script)
    {
        var comments = new List<(int, string)>();
        string? hereDocumentEnd = null;
        char? quote = null;
        var lines = script.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (hereDocumentEnd is not null)
            {
                hereDocumentEnd = line.Trim() == hereDocumentEnd ? null : hereDocumentEnd;
                continue;
            }
            if (index == 0 && line.StartsWith("#!", StringComparison.Ordinal))
            {
                continue;
            }
            var commentStart = -1;
            for (var at = 0; at < line.Length && commentStart < 0; at++)
            {
                var sign = line[at];
                if (quote is not null)
                {
                    if (sign == '\\' && quote == '"')
                    {
                        at++;
                    }
                    else if (sign == quote)
                    {
                        quote = null;
                    }
                }
                else if (sign == '\\')
                {
                    at++;
                }
                else if (sign is '"' or '\'')
                {
                    quote = sign;
                }
                else if (sign == '#' && (at == 0 || char.IsWhiteSpace(line[at - 1])))
                {
                    commentStart = at;
                }
            }
            if (commentStart >= 0)
            {
                comments.Add((index + 1, line[commentStart..].TrimEnd()));
            }
            var code = commentStart >= 0 ? line[..commentStart] : line;
            var document = HereDocument().Match(code);
            if (document.Success && quote is null)
            {
                hereDocumentEnd = document.Groups[1].Value;
            }
        }
        return comments;
    }
}
