using System.Text.Json.Nodes;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Tests.Files;

public sealed class YamlSourceTests
{
    [Fact]
    public void Plain_scalars_are_typed_and_quoted_ones_stay_text()
    {
        var problems = new List<Problem>();

        var source = YamlSource.Read("s.yaml", "a: 1\nb: 1.5\nc: true\nd: ~\ne: \"1\"\nf: yes\ng: A-17\n", problems)!;

        Assert.Empty(problems);
        Assert.Equal( /*lang=json,strict*/
            """{"a":1,"b":1.5,"c":true,"d":null,"e":"1","f":"yes","g":"A-17"}""",
            source.Root!.ToJsonString()
        );
    }

    [Fact]
    public void A_problem_carries_the_line_and_column_of_its_path()
    {
        var problems = new List<Problem>();
        var source = YamlSource.Read("s.yaml", "key: a\nitems:\n  - x\n  - y: 2\n", problems)!;

        var problem = source.Problem("items[1].y", "boom");

        Assert.Equal((4L, 5L), (problem.Line, problem.Column));
        Assert.Equal("items[1].y", problem.Path);
    }

    [Fact]
    public void A_path_that_is_not_in_the_file_points_at_the_nearest_parent()
    {
        var problems = new List<Problem>();
        var source = YamlSource.Read("s.yaml", "key: a\nitems:\n  - x\n", problems)!;

        Assert.Equal(3L, source.Problem("items[0].missing", "boom").Line);
    }

    [Fact]
    public void An_alias_reports_the_path_the_file_has()
    {
        var problems = new List<Problem>();
        var source = YamlSource.Read("s.yaml", "conversation:\n  - user: hi\n", problems)!;
        source.AddAlias("conversation[0].text", "conversation[0].user");

        var problem = source.Problem("conversation[0].text", "boom");

        Assert.Equal("conversation[0].user", problem.Path);
        Assert.Equal(2L, problem.Line);
    }

    [Theory]
    [InlineData("a: [1, 2")]
    [InlineData("a: b: c")]
    [InlineData("a: 1\na: 2\n")]
    [InlineData("")]
    [InlineData("a: 1\n---\nb: 2\n")]
    public void A_file_that_is_not_one_YAML_document_is_a_problem(string text)
    {
        var problems = new List<Problem>();

        var source = YamlSource.Read("s.yaml", text, problems);

        Assert.Null(source);
        Assert.Single(problems);
    }

    [Fact]
    public void The_root_is_a_json_object_for_a_mapping()
    {
        var problems = new List<Problem>();

        Assert.IsType<JsonObject>(YamlSource.Read("s.yaml", "a: 1\n", problems)!.Root);
    }
}
