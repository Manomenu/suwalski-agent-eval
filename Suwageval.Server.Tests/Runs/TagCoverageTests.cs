using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Tests.Runs;

public sealed class TagCoverageTests
{
    private static readonly DateTime RunTime = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private static ScenarioLifetime Scenario(string key, string[] tags, int createdDay = 1, int? deletedDay = null) =>
        new()
        {
            Key = key,
            Tags = tags,
            CreatedAt = new DateTime(2026, 1, createdDay, 0, 0, 0, DateTimeKind.Utc),
            DeletedAt = deletedDay is { } day ? new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc) : null,
        };

    private static readonly ScenarioLifetime[] Showcase =
    [
        Scenario("exact-address", ["mediacom", "serviceability"]),
        Scenario("other-address", ["mediacom"]),
        Scenario("outage", ["serviceability", "outage-only"]),
    ];

    [Fact]
    public void A_run_of_every_scenario_covers_every_tag() =>
        Assert.Equal(
            ["mediacom", "outage-only", "serviceability"],
            TagCoverage.CompleteTags(RunTime, ["exact-address", "other-address", "outage"], Showcase).Order()
        );

    [Fact]
    public void The_counts_say_how_many_scenarios_of_a_tag_the_run_ran_and_how_many_the_tag_had() =>
        Assert.Equal((1, 2), TagCoverage.Counts("mediacom", RunTime, ["exact-address", "outage"], Showcase));

    [Fact]
    public void A_run_limited_to_a_tag_leaves_out_another_tag_it_ran_in_part() =>
        Assert.DoesNotContain("mediacom", TagCoverage.CompleteTags(RunTime, ["exact-address", "outage"], Showcase));

    [Fact]
    public void A_run_limited_to_a_tag_keeps_another_tag_whose_scenarios_all_lie_inside_it() =>
        Assert.Equal(["outage-only", "serviceability"], TagCoverage.CompleteTags(RunTime, ["exact-address", "outage"], Showcase).Order());

    [Fact]
    public void Only_the_scenarios_that_existed_when_the_run_was_queued_count()
    {
        ScenarioLifetime[] scenarios =
        [
            Scenario("old", ["billing"]),
            Scenario("added-later", ["billing"], createdDay: 20),
            Scenario("deleted-before", ["billing"], deletedDay: 5),
            Scenario("deleted-later", ["billing"], deletedDay: 20),
        ];

        Assert.Equal(["billing"], TagCoverage.CompleteTags(RunTime, ["old", "deleted-later"], scenarios));
        Assert.Empty(TagCoverage.CompleteTags(RunTime, ["old"], scenarios));
    }
}
