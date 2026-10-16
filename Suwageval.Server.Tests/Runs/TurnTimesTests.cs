using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class TurnTimesTests
{
    [Fact]
    public void Median_takes_the_middle_value_or_the_mean_of_the_two_middle_ones()
    {
        Assert.Equal(3.0, TurnTimes.Median([5, 1, 3]));
        Assert.Equal(2.5, TurnTimes.Median([4, 1, 2, 3]));
        Assert.Null(TurnTimes.Median([]));
    }

    [Fact]
    public void P95_is_the_value_at_rank_ceiling_of_ninety_five_percent()
    {
        Assert.Equal(19.0, TurnTimes.P95([.. Enumerable.Range(1, 20)]));
        Assert.Equal(4.0, TurnTimes.P95([4, 1, 2, 3]));
        Assert.Equal(7.0, TurnTimes.P95([7]));
        Assert.Null(TurnTimes.P95([]));
    }
}
