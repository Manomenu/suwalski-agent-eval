using Suwageval.Contracts.Agents;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Tests.Agents;

public sealed class LineDiffTests
{
    private static string Lines(int count, string prefix) =>
        string.Concat(Enumerable.Range(0, count).Select(index => $"{prefix}{index}\n"));

    [Fact]
    public void Rows_number_the_lines_of_each_side_and_put_removed_before_added()
    {
        var rows = LineDiff.Rows("a\nb\nc\n", "a\nx\nc\n");

        Assert.Equal(
            [(DiffKind.Same, "a", 1, 1), (DiffKind.Removed, "b", 2, null), (DiffKind.Added, "x", null, 2), (DiffKind.Same, "c", 3, 3)],
            rows.Select(row => (row.Kind, row.Text, row.FromLine, row.ToLine))
        );
    }

    [Fact]
    public void Rows_of_an_empty_text_are_only_the_other_sides_lines()
    {
        Assert.Equal([DiffKind.Added, DiffKind.Added], LineDiff.Rows("", "a\nb\n").Select(row => row.Kind));
        Assert.Equal([DiffKind.Removed], LineDiff.Rows("a\n", "").Select(row => row.Kind));
        Assert.Empty(LineDiff.Rows("", ""));
    }

    [Fact]
    public void Blocks_are_none_for_equal_texts() => Assert.Empty(LineDiff.Blocks(LineDiff.Rows("a\nb\n", "a\nb\n")));

    [Fact]
    public void Blocks_keep_three_lines_of_context_and_fold_the_rest_of_a_long_unchanged_run()
    {
        var rows = LineDiff.Rows($"{Lines(10, "top")}old\n{Lines(10, "bottom")}", $"{Lines(10, "top")}new\n{Lines(10, "bottom")}");

        var blocks = LineDiff.Blocks(rows);

        Assert.Equal([(true, 7), (false, 3), (false, 2), (false, 3), (true, 7)], blocks.Select(block => (block.Folded, block.Rows.Count)));
        Assert.Equal(["top7", "top8", "top9"], blocks[1].Rows.Select(row => row.Text));
    }

    [Fact]
    public void Blocks_fold_the_run_between_two_changes_only_past_six_lines_and_never_a_single_line()
    {
        static IReadOnlyList<DiffBlock> Make(int middle)
        {
            return LineDiff.Blocks(LineDiff.Rows($"x\n{Lines(middle, "m")}y\n", $"X\n{Lines(middle, "m")}Y\n"));
        }

        Assert.DoesNotContain(Make(6), block => block.Folded);
        Assert.DoesNotContain(Make(7), block => block.Folded);
        Assert.Equal([2], Make(8).Where(block => block.Folded).Select(block => block.Rows.Count));
    }
}
