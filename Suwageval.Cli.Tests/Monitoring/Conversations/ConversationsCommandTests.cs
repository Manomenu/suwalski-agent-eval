using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Monitoring;

namespace Suwageval.Cli.Tests.Monitoring.Conversations;

/// <summary><c>suwageval monitoring conversations</c> and <c>conversation</c> against a stand-in for the API.</summary>
public sealed class ConversationsCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly Guid Version = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static readonly ConversationSummary Chat = new()
    {
        Id = Guid.Parse("cccccccc-0000-0000-0000-000000000001"),
        Agent = "example-agent",
        Environment = "production",
        ConversationId = "chat-81",
        AgentVersionId = Version,
        VersionLabel = "v7",
        VersionChanged = true,
        State = ConversationState.Closed,
        Turns = 2,
        StartedAt = DateTimeOffset.Parse("2026-10-24T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        LastTurnAt = DateTimeOffset.Parse("2026-10-24T12:05:00Z", System.Globalization.CultureInfo.InvariantCulture),
    };

    private static (int Exit, string Output, string Error, List<string> Paths) Run(object answer, params string[] args)
    {
        var paths = new List<string>();
        using var handler = new Answer(paths, answer);
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["monitoring", .. args], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), paths);
    }

    private sealed class Answer(List<string> paths, object answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            paths.Add(request.RequestUri?.PathAndQuery ?? "");
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(answer, ContractJson.Options), Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    private static ConversationList Page(params ConversationSummary[] conversations) =>
        new()
        {
            Conversations = conversations,
            Page = 1,
            TotalCount = conversations.Length,
            Environments = ["production"],
        };

    [Fact]
    public void The_conversations_of_the_agent_in_agent_yaml_are_listed_one_per_line_with_the_default_limit()
    {
        var (exit, output, error, paths) = Run(Page(Chat), "conversations", "--dir", Sample);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(["/monitoring/agents/example-agent/conversations?pageSize=20"], paths);
        Assert.Equal(
            $"{Chat.Id}  production  Closed  2 turns  version {Version}  label v7 (changed during it)  last 2026-10-24 12:05Z  not checked  conversation chat-81",
            output.Trim()
        );
    }

    [Fact]
    public void The_filters_go_to_the_server_and_an_empty_page_says_so()
    {
        var (exit, output, _, paths) = Run(
            Page(),
            "conversations",
            "--dir",
            Sample,
            "--environment",
            "staging",
            "--version",
            Version.ToString(),
            "--state",
            "Open",
            "--limit",
            "5"
        );

        Assert.Equal(0, exit);
        Assert.Equal(
            [$"/monitoring/agents/example-agent/conversations?pageSize=5&environment=staging&version={Version}&state=Open"],
            paths
        );
        Assert.Equal("No conversations.", output.Trim());
    }

    [Fact]
    public void Failed_check_filters_and_the_line_counts_the_failed_checks()
    {
        var failed = Chat with { CheckedAt = Chat.LastTurnAt, FailedChecks = 2, NotJudgedChecks = 1 };

        var (exit, output, _, paths) = Run(Page(failed), "conversations", "--dir", Sample, "--failed-check", "no-refund");

        Assert.Equal(0, exit);
        Assert.Equal(["/monitoring/agents/example-agent/conversations?pageSize=20&failedCheck=no-refund"], paths);
        Assert.Contains("  2 checks failed, 1 check not judged  ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_limit_out_of_range_is_an_error_before_any_request()
    {
        var (exit, _, error, paths) = Run(Page(), "conversations", "--dir", Sample, "--limit", "0");

        Assert.NotEqual(0, exit);
        Assert.Contains("--limit is from 1 to 100.", error, StringComparison.Ordinal);
        Assert.Empty(paths);
    }

    [Fact]
    public void One_conversation_is_printed_with_its_turns_and_their_items()
    {
        var detail = new ConversationDetail
        {
            Conversation = Chat,
            Turns =
            [
                new ConversationTurn
                {
                    Id = Guid.NewGuid(),
                    At = Chat.StartedAt,
                    AgentVersionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"),
                    Items = [new UserMessage { Text = "Where is A-17?" }, new AssistantMessage { Text = "On its way." }],
                },
                new ConversationTurn
                {
                    Id = Guid.NewGuid(),
                    At = Chat.LastTurnAt,
                    AgentVersionId = Version,
                    DataVersion = "index-7",
                    Items = [new UserMessage { Text = "Thanks" }, new AssistantMessage { Text = "You're welcome." }],
                },
            ],
            Checks =
            [
                new ConversationCheckResult
                {
                    Key = "no-refund",
                    Revision = 1,
                    Passed = false,
                    Message = "refund was called",
                },
            ],
        };

        var (exit, output, _, paths) = Run(detail, "conversation", Chat.Id.ToString());

        Assert.Equal(0, exit);
        Assert.Equal([$"/monitoring/conversations/{Chat.Id}"], paths);
        var lines = output.ReplaceLineEndings("\n").Split('\n');
        Assert.Equal("✗ no-refund: refund was called", lines[1]);
        lines = [.. lines.Where((_, index) => index != 1)];
        Assert.Equal("turn 1  2026-10-24 12:00:00Z  version bbbbbbbb-0000-0000-0000-000000000002", lines[2]);
        Assert.Equal("  user: Where is A-17?", lines[3]);
        Assert.Equal("  assistant: On its way.", lines[4]);
        Assert.Equal("turn 2  2026-10-24 12:05:00Z  data index-7", lines[6]);
    }

    [Fact]
    public void Open_opens_the_monitoring_page_narrowed_to_the_environment_and_the_failed_check()
    {
        var (exit, output, error, opened, paths) = OpenedPage.Run(
            "{}",
            "monitoring",
            "conversations",
            "--environment",
            "production",
            "--failed-check",
            "no-refund",
            "--dir",
            OpenedPage.Sample
        );

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(
            [new Uri("https://eval.example/agents/example-agent/monitoring?failedCheck=no-refund&environment=production#conversations")],
            opened
        );
        Assert.Equal(
            "https://eval.example/agents/example-agent/monitoring?failedCheck=no-refund&environment=production#conversations"
                + Environment.NewLine,
            output
        );
        Assert.Empty(paths);
    }

    [Fact]
    public void Open_opens_the_conversations_page()
    {
        var detail = JsonSerializer.Serialize(
            new ConversationDetail
            {
                Conversation = Chat,
                Turns = [],
                Checks = [],
            },
            ContractJson.Options
        );

        var (exit, output, error, opened, paths) = OpenedPage.Run(detail, "monitoring", "conversation", Chat.Id.ToString());

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal([new Uri($"https://eval.example/conversations/{Chat.Id}")], opened);
        Assert.Equal($"https://eval.example/conversations/{Chat.Id}" + Environment.NewLine, output);
        Assert.Equal([$"/monitoring/conversations/{Chat.Id}"], paths);
    }
}
