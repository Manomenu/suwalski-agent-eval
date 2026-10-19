using System.Globalization;
using System.Text;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Versions;

/// <summary>What <c>suwageval versions diff</c> prints: the two versions, then the changed hunks as a unified-style diff.</summary>
internal static class VersionDiffText
{
    public static string Describe(VersionDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var text = new StringBuilder();
        text.AppendLine(Header("---", diff.From)).AppendLine(Header("+++", diff.To));
        if (diff.Blocks.Count == 0)
        {
            return text.Append("No changes.").ToString();
        }
        foreach (var block in diff.Blocks)
        {
            if (block.Folded)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"… {block.Rows.Count} unchanged lines"));
                continue;
            }
            text.AppendLine("@@");
            foreach (var row in block.Rows)
            {
                var prefix = row.Kind switch
                {
                    DiffKind.Added => '+',
                    DiffKind.Removed => '-',
                    DiffKind.Same => ' ',
                    _ => ' ',
                };
                text.Append(prefix).AppendLine(row.Text);
            }
        }
        return text.Append(string.Create(CultureInfo.InvariantCulture, $"+{diff.Added} −{diff.Removed} lines")).ToString();
    }

    private static string Header(string mark, AgentVersion version) =>
        $"{mark} version {version.Id}{(version.Label is null ? "" : $" ({version.Label})")}";
}
