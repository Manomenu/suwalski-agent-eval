using Suwageval.Cli.Evaluation.Sweep;

namespace Suwageval.Cli.Tests.Evaluation.Sweep;

public sealed class VariantSelectionTests
{
    private static readonly string[] Variants = ["a", "b", "c", "d", "e"];

    [Fact]
    public void Numbers_ranges_and_all_pick_variants_in_list_order_without_repeats()
    {
        Assert.Equal(["a", "c", "e"], VariantSelection.Parse("5, 1 3,3", Variants, out _));
        Assert.Equal(["b", "c", "d", "e"], VariantSelection.Parse("2-4,5", Variants, out _));
        Assert.Equal(Variants, VariantSelection.Parse(" ALL ", Variants, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("4-2")]
    [InlineData("1-9")]
    [InlineData("1-2-3")]
    [InlineData("x")]
    [InlineData("-1")]
    [InlineData("all,1")]
    public void An_answer_that_picks_nothing_or_something_outside_the_list_is_refused_with_a_reason(string answer)
    {
        Assert.Null(VariantSelection.Parse(answer, Variants, out var problem));
        Assert.NotEmpty(problem);
    }
}
