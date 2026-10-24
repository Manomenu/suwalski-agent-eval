using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Evaluation.Groups;

/// <summary>The commands against a stand-in for the API.</summary>
public sealed class GroupsCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private sealed record Call(HttpMethod Method, string Path, string Body);

    private sealed class FakeApi : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new Call(request.Method, request.RequestUri?.AbsolutePath ?? "", body));
            return request.Method switch
            {
                var method when method == HttpMethod.Get => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new RunGroupDeletion("sweep-1", 3, 12, 36), ContractJson.Options),
                        Encoding.UTF8,
                        "application/json"
                    ),
                },
                _ => new HttpResponseMessage(HttpStatusCode.NoContent),
            };
        }
    }

    private static (int Exit, string Output, string Error, FakeApi Api) Groups(string typed, bool interactive, params string[] args)
    {
        var api = new FakeApi();
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["groups", .. args, "--dir", Sample], new StringReader(typed), output, error, http, interactive: interactive);
        return (exit, output.ToString(), error.ToString(), api);
    }

    [Theory]
    [InlineData("archive", true)]
    [InlineData("unarchive", false)]
    public void Archive_and_unarchive_post_the_names_with_the_flag(string verb, bool archived)
    {
        var (exit, output, error, api) = Groups("", false, verb, "sweep-1", "sweep-2");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Contains($"{verb}d 2 group(s)", output, StringComparison.Ordinal);
        var call = api.Calls.Single();
        Assert.Equal((HttpMethod.Post, "/agents/example-agent/run-groups/archive"), (call.Method, call.Path));
        var sent = JsonSerializer.Deserialize<ArchiveRunGroupsRequest>(call.Body, ContractJson.Options);
        Assert.Equal(["sweep-1", "sweep-2"], sent?.Names);
        Assert.Equal(archived, sent?.Archived);
    }

    [Fact]
    public void Delete_dry_run_prints_what_would_go_and_deletes_nothing()
    {
        var (exit, output, _, api) = Groups("", true, "delete", "sweep-1", "--dry-run");

        Assert.Equal(0, exit);
        Assert.Contains("would delete: 3 run(s), 12 sample(s), 36 verdict(s)", output, StringComparison.Ordinal);
        Assert.Equal(
            (HttpMethod.Get, "/agents/example-agent/run-groups/sweep-1/deletion"),
            (api.Calls.Single().Method, api.Calls.Single().Path)
        );
    }

    [Fact]
    public void Delete_in_a_terminal_deletes_when_the_name_is_typed()
    {
        var (exit, output, _, api) = Groups("sweep-1\n", true, "delete", "sweep-1");

        Assert.Equal(0, exit);
        Assert.Contains("Type the group name to confirm", output, StringComparison.Ordinal);
        Assert.Equal((HttpMethod.Delete, "/agents/example-agent/run-groups/sweep-1"), (api.Calls[^1].Method, api.Calls[^1].Path));
    }

    [Theory]
    [InlineData("yes\n", true)]
    [InlineData("sweep-1\n", false)]
    public void Delete_with_anything_but_the_name_or_without_a_terminal_exits_1_and_deletes_nothing(string typed, bool interactive)
    {
        var (exit, _, error, api) = Groups(typed, interactive, "delete", "sweep-1");

        Assert.Equal(1, exit);
        Assert.NotEmpty(error);
        Assert.DoesNotContain(api.Calls, call => call.Method == HttpMethod.Delete);
    }

    [Fact]
    public void Delete_with_yes_deletes_without_asking()
    {
        var (exit, output, _, api) = Groups("", false, "delete", "sweep-1", "--yes");

        Assert.Equal(0, exit);
        Assert.Contains("deleted group sweep-1", output, StringComparison.Ordinal);
        Assert.Contains(api.Calls, call => call.Method == HttpMethod.Delete);
    }
}
