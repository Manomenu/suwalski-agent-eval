using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Tests.Versions;

/// <summary>The commands against a stand-in for the API.</summary>
public sealed class VersionsCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly Guid Active = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Archived = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private sealed record Call(HttpMethod Method, string Path, string Body);

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new Call(request.Method, request.RequestUri?.AbsolutePath ?? "", body));
            return answer(request);
        }
    }

    private static HttpResponseMessage Versions() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new[]
                    {
                        new AgentVersion(
                            Active,
                            Guid.NewGuid(),
                            "v2",
                            "hash2",
                            DateTimeOffset.Parse("2026-10-02T10:00:00Z"),
                            null,
                            ["m"],
                            null,
                            null
                        ),
                        new AgentVersion(
                            Archived,
                            Guid.NewGuid(),
                            null,
                            "hash1",
                            DateTimeOffset.Parse("2026-10-01T10:00:00Z"),
                            DateTimeOffset.Parse("2026-10-03T10:00:00Z"),
                            ["m"],
                            "2026-10-01",
                            "1832 articles"
                        ),
                    },
                    ContractJson.Options
                ),
                Encoding.UTF8,
                "application/json"
            ),
        };

    private static (int Exit, string Output, string Error, FakeApi Api) Versions(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        params string[] args
    )
    {
        var api = new FakeApi(answer);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["versions", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), api);
    }

    [Fact]
    public void It_lists_the_active_versions_of_the_agent_in_agent_yaml()
    {
        var (exit, output, error, api) = Versions(_ => Versions());

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("/agents/example-agent/versions", api.Calls.Single().Path);
        Assert.Contains(Active.ToString(), output, StringComparison.Ordinal);
        Assert.Contains("v2", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Archived.ToString(), output, StringComparison.Ordinal);
    }

    [Fact]
    public void Archived_adds_the_archived_versions_marked()
    {
        var (_, output, _, _) = Versions(_ => Versions(), "--archived");

        var archived = output.Split('\n').Single(line => line.Contains(Archived.ToString(), StringComparison.Ordinal));
        Assert.EndsWith("archived", archived.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("active", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_prints_the_versions_as_the_server_sent_them()
    {
        var (_, output, _, _) = Versions(_ => Versions(), "--json", "--archived");

        var printed = JsonSerializer.Deserialize<AgentVersion[]>(output, ContractJson.Options);
        Assert.Equal([Active, Archived], printed?.Select(version => version.Id));
        Assert.NotNull(printed?[1].ArchivedAt);
    }

    [Theory]
    [InlineData("archive", true)]
    [InlineData("unarchive", false)]
    public void Archive_and_unarchive_post_the_ids_with_the_flag(string verb, bool archived)
    {
        var (exit, output, error, api) = Versions(
            _ => new HttpResponseMessage(HttpStatusCode.NoContent),
            verb,
            Active.ToString(),
            Archived.ToString()
        );

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Contains(verb, output, StringComparison.Ordinal);
        var call = api.Calls.Single();
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("/agents/example-agent/versions/archive", call.Path);
        var sent = JsonSerializer.Deserialize<ArchiveVersionsRequest>(call.Body, ContractJson.Options);
        Assert.Equal([Active, Archived], sent?.VersionIds);
        Assert.Equal(archived, sent?.Archived);
    }

    [Fact]
    public void A_version_the_server_does_not_know_exits_2_with_its_message()
    {
        var (exit, output, error, _) = Versions(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    /*lang=json,strict*/
                    """{"title":"Version not found","detail":"There is no version 'x'."}""",
                    Encoding.UTF8,
                    "application/problem+json"
                ),
            },
            "archive",
            Active.ToString()
        );

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("There is no version 'x'.", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_server_that_does_not_answer_exits_2()
    {
        var (exit, _, error, _) = Versions(_ => throw new HttpRequestException("connection refused"));

        Assert.Equal(2, exit);
        Assert.Contains("could not be reached", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_agent_file_exits_2_without_calling_the_server()
    {
        using var api = new FakeApi(_ => Versions());
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = CliApp.Run(
            ["versions", "--dir", Path.Combine(Sample, "nowhere")],
            new StringReader(""),
            output,
            error,
            http,
            interrupt: TestContext.Current.CancellationToken
        );

        Assert.Equal(2, exit);
        Assert.Contains("agent.yaml not found", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public void A_version_id_that_is_not_one_is_refused_before_any_request()
    {
        var (exit, _, _, api) = Versions(_ => Versions(), "archive", "not-a-version");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Calls);
    }

    private static (int Exit, string Output, string Error, FakeApi Api, List<Uri> Opened) Diff(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        params string[] args
    )
    {
        var api = new FakeApi(answer);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var opened = new List<Uri>();
        var exit = CliApp.Run(["versions", "diff", .. args, "--dir", Sample], new StringReader(""), output, error, http, opened.Add);
        return (exit, output.ToString(), error.ToString(), api, opened);
    }

    private static HttpResponseMessage Detail(HttpRequestMessage request)
    {
        var id = Guid.Parse(request.RequestUri?.AbsolutePath.Split('/').Last() ?? "");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new AgentVersionDetail(
                        id,
                        Guid.NewGuid(),
                        "hash",
                        DateTimeOffset.Parse("2026-10-01T10:00:00Z"),
                        null,
                        new AgentContract
                        {
                            Agents =
                            [
                                new LlmAgentDefinition
                                {
                                    Name = "main",
                                    Model = new ModelDefinition { Name = "m" },
                                    Config = "c",
                                },
                            ],
                        }
                    ),
                    ContractJson.Options
                ),
                Encoding.UTF8,
                "application/json"
            ),
        };
    }

    [Fact]
    public void Diff_checks_both_versions_then_prints_and_opens_the_page()
    {
        var (exit, output, error, api, opened) = Diff(Detail, Archived.ToString(), Active.ToString());

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(
            [$"/agents/example-agent/versions/{Archived}", $"/agents/example-agent/versions/{Active}"],
            api.Calls.Select(call => call.Path)
        );
        var page = $"http://localhost:3230/agents/example-agent/versions/compare?a={Archived}&b={Active}";
        Assert.Equal(page, output.Trim());
        Assert.Equal(page, opened.Single().ToString());
    }

    [Fact]
    public void Diff_of_a_version_the_agent_does_not_have_exits_2_and_opens_nothing()
    {
        var (exit, output, error, _, opened) = Diff(
            request =>
                request.RequestUri?.AbsolutePath.EndsWith(Active.ToString(), StringComparison.Ordinal) == true
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : Detail(request),
            Archived.ToString(),
            Active.ToString()
        );

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("404", error, StringComparison.Ordinal);
        Assert.Empty(opened);
    }

    [Fact]
    public void Diff_of_the_same_version_twice_exits_2_without_any_request()
    {
        var (exit, _, error, api, _) = Diff(Detail, Active.ToString(), Active.ToString());

        Assert.Equal(2, exit);
        Assert.Contains("two different versions", error, StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }
}
