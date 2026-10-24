using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Tests.Evaluation.Scenarios;

/// <summary>The <c>scenarios</c> and <c>scenario</c> commands against a stand-in for the API.</summary>
public sealed class ScenarioCommandsTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly DateTimeOffset Created = DateTimeOffset.Parse(
        "2026-10-02T10:00:00Z",
        System.Globalization.CultureInfo.InvariantCulture
    );

    private static ScenarioInput Refund(IReadOnlyList<string> tags) =>
        new()
        {
            Key = "refund",
            Tags = tags,
            Conversation =
            [
                new UserMessage { Text = "I want my money back.\nOrder 42." },
                new ToolCall
                {
                    Id = "c1",
                    Name = "refund",
                    Arguments = JsonDocument.Parse("""{"order":42}""").RootElement.Clone(),
                },
            ],
            Samples = 5,
        };

    private static readonly CheckDetail[] Checks =
    [
        new(
            2,
            Created,
            new ToolCalledCheck
            {
                Key = "refund-called",
                ScenarioKey = "refund",
                Tool = "refund",
                Args = JsonDocument.Parse("""{"order":42}""").RootElement.Clone(),
            }
        ),
        new(
            1,
            Created,
            new JudgeCheck
            {
                Key = "polite",
                ScenarioKey = "refund",
                Criteria = "The answer is polite.",
            }
        ),
        new(
            1,
            Created,
            new TextCheck
            {
                Key = "other",
                ScenarioKey = "other-scenario",
                Contains = "x",
            }
        ),
    ];

    private sealed class Answer(List<string> paths) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException();
            paths.Add(uri.PathAndQuery);
            object? body = uri.AbsolutePath switch
            {
                "/agents/example-agent/scenarios" => new ScenarioSummary[]
                {
                    new("refund", ["smoke", "refunds"], 2, Created),
                    new("other-scenario", [], 1, Created),
                },
                "/agents/example-agent/scenarios/refund" => new ScenarioDetail(2, Created, Refund(["smoke"])),
                "/agents/example-agent/scenarios/refund/revisions" => new ScenarioRevision[]
                {
                    new(1, "0123456789abcdef", Created.AddDays(-1), Refund([])),
                    new(2, "fedcba9876543210", Created, Refund([])),
                },
                "/agents/example-agent/checks" => uri.Query.Contains("scenario=refund", StringComparison.Ordinal)
                    ? [.. Checks.Take(2)]
                    : Checks,
                _ => null,
            };
            return Task.FromResult(
                body is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(body, ContractJson.Options),
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
            );
        }
    }

    private static (int Exit, string Output, string Error, List<string> Paths) Run(params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run([.. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), paths);
    }

    [Fact]
    public void Scenarios_lists_key_revision_check_count_and_tags()
    {
        var (exit, output, _, paths) = Run("scenarios");

        Assert.Equal(0, exit);
        Assert.Equal(["/agents/example-agent/scenarios", "/agents/example-agent/checks"], paths);
        Assert.Equal(
            ["refund  revision 2  2 checks  tags: smoke, refunds", "other-scenario  revision 1  1 check  tags: -"],
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        );
    }

    [Fact]
    public void Scenarios_passes_the_tag_to_the_server()
    {
        var (_, _, _, paths) = Run("scenarios", "--tag", "smoke");

        Assert.Equal("/agents/example-agent/scenarios?tag=smoke", paths[0]);
    }

    [Fact]
    public void Scenarios_json_prints_the_summaries_and_asks_nothing_more()
    {
        var (_, output, _, paths) = Run("scenarios", "--json");

        Assert.Equal(
            ["refund", "other-scenario"],
            JsonSerializer.Deserialize<ScenarioSummary[]>(output, ContractJson.Options)?.Select(s => s.Key)
        );
        Assert.Single(paths);
    }

    [Fact]
    public void Scenario_prints_conversation_checks_with_definitions_and_revisions()
    {
        var (exit, output, error, paths) = Run("scenario", "refund");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Contains("/agents/example-agent/checks?scenario=refund", paths);
        var text = output.ReplaceLineEndings("\n").TrimEnd();
        Assert.StartsWith("refund  revision 2  tags: smoke\nsamples: 5  turns: 1\n", text, StringComparison.Ordinal);
        Assert.Contains("  user: I want my money back.\n    Order 42.\n", text, StringComparison.Ordinal);
        Assert.Contains("  tool call refund {\"order\":42}\n", text, StringComparison.Ordinal);
        Assert.Contains("checks (2):\n  refund-called  revision 2\n    type: tool-called\n", text, StringComparison.Ordinal);
        Assert.Contains("    tool: refund\n", text, StringComparison.Ordinal);
        Assert.Contains("    args:\n      order: 42\n", text, StringComparison.Ordinal);
        Assert.Contains("  polite  revision 1\n    type: judge\n", text, StringComparison.Ordinal);
        Assert.Contains("    criteria: The answer is polite.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("other", text.Split("revisions:")[0].Split("checks (")[1], StringComparison.Ordinal);
        Assert.EndsWith("revisions:\n  2  2026-10-02 10:00Z  fedcba98\n  1  2026-10-01 10:00Z  01234567", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Scenario_json_prints_scenario_checks_and_revisions()
    {
        var (_, output, _, _) = Run("scenario", "refund", "--json");

        using var printed = JsonDocument.Parse(output);
        Assert.Equal("refund", printed.RootElement.GetProperty("scenario").GetProperty("scenario").GetProperty("key").GetString());
        Assert.Equal(2, printed.RootElement.GetProperty("checks").GetArrayLength());
        Assert.Equal(2, printed.RootElement.GetProperty("revisions").GetArrayLength());
    }

    [Fact]
    public void An_unknown_scenario_exits_2_with_the_servers_answer()
    {
        var (exit, output, error, _) = Run("scenario", "nope");

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("404", error, StringComparison.Ordinal);
    }
}
