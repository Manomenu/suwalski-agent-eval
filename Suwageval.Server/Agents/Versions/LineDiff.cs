using Suwageval.Contracts.Agents;

namespace Suwageval.Server.Agents.Versions;

/// <summary>A line diff of two texts: every line with its numbers on each side, and the hunks a reader sees (changes with their context, long unchanged runs folded).</summary>
internal static class LineDiff
{
    /// <summary>Unchanged lines kept around a change; a longer unchanged run folds.</summary>
    public const int Context = 3;

    /// <summary>Every line of both texts, in order, with its numbers on each side; within a change the removed lines come before the added ones.</summary>
    public static IReadOnlyList<DiffRow> Rows(string from, string to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var before = Lines(from);
        var after = Lines(to);

        // The common head and tail are unchanged whatever the middle is, and most contracts differ in a few lines only.
        var head = 0;
        while (head < before.Length && head < after.Length && before[head] == after[head])
        {
            head++;
        }
        var tail = 0;
        while (tail < before.Length - head && tail < after.Length - head && before[^(tail + 1)] == after[^(tail + 1)])
        {
            tail++;
        }
        var middleBefore = before[head..(before.Length - tail)];
        var middleAfter = after[head..(after.Length - tail)];

        var rows = new List<DiffRow>();
        var fromLine = 1;
        var toLine = 1;
        void Add(DiffKind kind, string text)
        {
            rows.Add(
                new DiffRow
                {
                    Kind = kind,
                    Text = text,
                    FromLine = kind == DiffKind.Added ? null : fromLine,
                    ToLine = kind == DiffKind.Removed ? null : toLine,
                }
            );
            fromLine += kind == DiffKind.Added ? 0 : 1;
            toLine += kind == DiffKind.Removed ? 0 : 1;
        }

        foreach (var line in before[..head])
        {
            Add(DiffKind.Same, line);
        }

        // Longest common subsequence of the middles, read from the end so that a walk from the start can follow it.
        var common = new int[middleBefore.Length + 1][];
        for (var i = 0; i <= middleBefore.Length; i++)
        {
            common[i] = new int[middleAfter.Length + 1];
        }
        for (var i = middleBefore.Length - 1; i >= 0; i--)
        {
            for (var j = middleAfter.Length - 1; j >= 0; j--)
            {
                common[i][j] = middleBefore[i] == middleAfter[j] ? common[i + 1][j + 1] + 1 : Math.Max(common[i + 1][j], common[i][j + 1]);
            }
        }
        int a = 0,
            b = 0;
        while (a < middleBefore.Length || b < middleAfter.Length)
        {
            if (a < middleBefore.Length && b < middleAfter.Length && middleBefore[a] == middleAfter[b])
            {
                Add(DiffKind.Same, middleBefore[a++]);
                b++;
            }
            else if (a < middleBefore.Length && (b == middleAfter.Length || common[a + 1][b] >= common[a][b + 1]))
            {
                Add(DiffKind.Removed, middleBefore[a++]);
            }
            else
            {
                Add(DiffKind.Added, middleAfter[b++]);
            }
        }

        foreach (var line in before[(before.Length - tail)..])
        {
            Add(DiffKind.Same, line);
        }
        return rows;
    }

    /// <summary>
    /// The hunks: every change with <paramref name="context"/> unchanged lines around it. A longer unchanged run folds into one block
    /// (a run that would hide a single line stays as it is); texts with no change give no blocks at all.
    /// </summary>
    public static IReadOnlyList<DiffBlock> Blocks(IReadOnlyList<DiffRow> rows, int context = Context)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.All(row => row.Kind == DiffKind.Same))
        {
            return [];
        }
        var blocks = new List<DiffBlock>();
        var index = 0;
        while (index < rows.Count)
        {
            var start = index;
            var unchanged = rows[index].Kind == DiffKind.Same;
            while (index < rows.Count && rows[index].Kind == DiffKind.Same == unchanged)
            {
                index++;
            }
            var run = rows.Skip(start).Take(index - start).ToList();
            var keepBefore = start == 0 ? 0 : context;
            var keepAfter = index == rows.Count ? 0 : context;
            var hidden = run.Count - keepBefore - keepAfter;
            if (!unchanged || hidden < 2)
            {
                blocks.Add(new DiffBlock { Folded = false, Rows = run });
                continue;
            }
            if (keepBefore > 0)
            {
                blocks.Add(new DiffBlock { Folded = false, Rows = run[..keepBefore] });
            }
            blocks.Add(new DiffBlock { Folded = true, Rows = run[keepBefore..(run.Count - keepAfter)] });
            if (keepAfter > 0)
            {
                blocks.Add(new DiffBlock { Folded = false, Rows = run[(run.Count - keepAfter)..] });
            }
        }
        return blocks;
    }

    private static string[] Lines(string text)
    {
        var lines = text.Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}
