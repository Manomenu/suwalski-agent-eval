using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Tests.Versions;

/// <summary>The commands against a stand-in for the API.</summary>
public sealed class VersionsCommandTests
{
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static readonly Guid Active = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Archived = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Ordinary = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid Mixed = Guid.Parse("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid Unused = Guid.Parse("eeeeeeee-0000-0000-0000-000000000005");

    private sealed record Call(HttpMethod Method, string Path, string Body, string Query = "");

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new Call(request.Method, request.RequestUri?.AbsolutePath ?? "", body, request.RequestUri?.Query ?? ""));
            return answer(request);
        }
    }

    /// <summary>One version of each usage: in evaluation, in both, only in groups (<see cref="Active"/>), unused and archived (with runs).</summary>
    private static HttpResponseMessage UsageVersions()
    {
        static AgentVersion Version(Guid id, int runs, int groupRuns, DateTimeOffset? archivedAt = null)
        {
            return new(id, Guid.NewGuid(), "hash", DateTimeOffset.Parse("2026-10-02T10:00:00Z"), ["m"], null, archivedAt)
            {
                GroupRunsOnly = runs == 0 && groupRuns > 0,
                Runs = runs,
                GroupRuns = groupRuns,
            };
        }
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new[]
                    {
                        Version(Ordinary, 2, 0),
                        Version(Mixed, 1, 1),
                        Version(Active, 0, 1),
                        Version(Unused, 0, 0),
                        Version(Archived, 3, 0, DateTimeOffset.Parse("2026-10-03T10:00:00Z")),
                    },
                    ContractJson.Options
                ),
                Encoding.UTF8,
                "application/json"
            ),
        };
    }

    private static HttpResponseMessage Versions() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new[]
                    {
                        new AgentVersion(Active, Guid.NewGuid(), "hash2", DateTimeOffset.Parse("2026-10-02T10:00:00Z"), ["m"], "v2")
                        {
                            GroupRunsOnly = true,
                            Runs = 0,
                            GroupRuns = 1,
                        },
                        new AgentVersion(
                            Archived,
                            Guid.NewGuid(),
                            "hash1",
                            DateTimeOffset.Parse("2026-10-01T10:00:00Z"),
                            ["m"],
                            null,
                            DateTimeOffset.Parse("2026-10-03T10:00:00Z"),
                            "2026-10-01",
                            "1832 articles"
                        )
                        {
                            GroupRunsOnly = false,
                            Runs = 1,
                            GroupRuns = 0,
                        },
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
    public void It_lists_the_versions_in_evaluation_and_the_unused_ones_of_the_agent_in_agent_yaml()
    {
        var (exit, output, error, api) = Versions(_ => UsageVersions());

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("/agents/example-agent/versions", api.Calls.Single().Path);
        Assert.Equal(
            [Ordinary.ToString(), Mixed.ToString(), Unused.ToString()],
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split("  ")[0])
        );
        Assert.Contains("runs 2  group runs 0", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("evaluation", "Ordinary,Mixed")]
    [InlineData("groups", "Mixed,GroupOnly")]
    [InlineData("unused", "Unused")]
    [InlineData("archived", "Archived")]
    [InlineData("evaluation,groups", "Ordinary,Mixed,GroupOnly")]
    [InlineData("unused,archived", "Unused,Archived")]
    [InlineData("Groups,UNUSED", "Mixed,GroupOnly,Unused")]
    public void A_version_is_listed_when_it_has_any_of_the_usages_and_an_archived_one_only_when_archived_is_chosen(
        string usage,
        string expected
    )
    {
        ArgumentNullException.ThrowIfNull(expected);
        var (exit, output, _, api) = Versions(_ => UsageVersions(), "--usage", usage);

        Assert.Equal(0, exit);
        Assert.Equal("", api.Calls.Single().Query);
        Assert.Equal(expected.Split(',').Order(), Listed(output).Select(NameOf).Order());
    }

    [Fact]
    public void Usage_can_be_repeated()
    {
        var (_, output, _, _) = Versions(_ => UsageVersions(), "--usage", "unused", "--usage", "archived");

        Assert.Equal(["Archived", "Unused"], Listed(output).Select(NameOf).Order());
    }

    [Fact]
    public void Version_narrows_the_list_within_the_usage_and_can_be_repeated()
    {
        var (_, output, _, _) = Versions(
            _ => UsageVersions(),
            "--usage",
            "evaluation,groups",
            "--version",
            Mixed.ToString(),
            "--version",
            Unused.ToString()
        );

        Assert.Equal(["Mixed"], Listed(output).Select(NameOf));
    }

    [Theory]
    [InlineData("--archived")]
    [InlineData("--any-group")]
    [InlineData("--all-versions")]
    public void Usage_cannot_be_used_with_the_older_options(string older)
    {
        var (exit, _, error, api) = Versions(_ => UsageVersions(), "--usage", "evaluation", older);

        Assert.NotEqual(0, exit);
        Assert.Contains("--usage cannot be used with", error, StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public void An_unknown_usage_is_refused_naming_the_four()
    {
        var (exit, _, error, api) = Versions(_ => UsageVersions(), "--usage", "forever");

        Assert.NotEqual(0, exit);
        Assert.Contains("'forever' is not a usage: use evaluation, groups, unused or archived.", error, StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public void The_text_shows_the_runs_and_the_group_runs()
    {
        var (_, output, _, _) = Versions(_ => UsageVersions(), "--usage", "groups");

        var mixed = Listed(output).Single(line => line.StartsWith(Mixed.ToString(), StringComparison.Ordinal));
        Assert.EndsWith("active  runs 1  group runs 1", mixed.TrimEnd(), StringComparison.Ordinal);
    }

    private static string[] Listed(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string NameOf(string line) =>
        Guid.Parse(line.Split("  ")[0]) switch
        {
            var id when id == Ordinary => "Ordinary",
            var id when id == Mixed => "Mixed",
            var id when id == Active => "GroupOnly",
            var id when id == Unused => "Unused",
            _ => "Archived",
        };

    [Theory]
    [InlineData("--archived", "?grouping=None")]
    [InlineData("--any-group", "?grouping=Any")]
    [InlineData("--all-versions", "")]
    public void The_grouping_asked_of_the_server_follows_any_group_and_all_versions(string option, string query)
    {
        ArgumentNullException.ThrowIfNull(option);
        var (exit, _, _, api) = Versions(_ => Versions(), option.Length == 0 ? [] : [option]);

        Assert.Equal(0, exit);
        Assert.Equal(query, api.Calls.Single().Query);
    }

    [Fact]
    public void Any_group_and_all_versions_cannot_be_used_together()
    {
        var (exit, _, _, api) = Versions(_ => Versions(), "--any-group", "--all-versions");

        Assert.NotEqual(0, exit);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public void A_version_whose_every_run_is_in_a_group_is_marked_and_archived_stays_separate()
    {
        var (_, output, _, _) = Versions(_ => Versions(), "--all-versions", "--archived");

        var lines = output.Split('\n');
        var marked = lines.Single(line => line.Contains(Active.ToString(), StringComparison.Ordinal));
        Assert.EndsWith("active  runs 0  group runs 1  group runs only", marked.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "group runs only",
            lines.Single(line => line.Contains(Archived.ToString(), StringComparison.Ordinal)),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Json_carries_group_runs_only()
    {
        var (_, output, _, _) = Versions(_ => Versions(), "--json", "--archived");

        var printed = JsonSerializer.Deserialize<AgentVersion[]>(output, ContractJson.Options);
        Assert.Equal([true, false], printed?.Select(v => v.GroupRunsOnly));
        Assert.Equal([0, 1], printed?.Select(v => v.Runs));
        Assert.Equal([1, 0], printed?.Select(v => v.GroupRuns));
    }

    [Fact]
    public void Archived_adds_the_archived_versions_marked()
    {
        var (_, output, _, _) = Versions(_ => Versions(), "--archived");

        var archived = output.Split('\n').Single(line => line.Contains(Archived.ToString(), StringComparison.Ordinal));
        Assert.EndsWith("archived  runs 1  group runs 0", archived.TrimEnd(), StringComparison.Ordinal);
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
        var exit = CliApp.Run(
            ["versions", "diff", "--open", .. args, "--dir", Sample],
            new StringReader(""),
            output,
            error,
            http,
            opened.Add
        );
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

    [Fact]
    public void Show_prints_the_version_with_its_contract()
    {
        var (exit, output, _, api) = Versions(Detail, "show", Active.ToString());

        Assert.Equal(0, exit);
        Assert.Equal([$"/agents/example-agent/versions/{Active}"], api.Calls.Select(call => call.Path));
        Assert.StartsWith($"version {Active}  created 2026-10-01 10:00Z", output, StringComparison.Ordinal);
        Assert.Contains("\"agents\"", output, StringComparison.Ordinal);
    }

    private static VersionDiff SampleDiff(bool changed)
    {
        static AgentVersion Version(Guid id, string? label)
        {
            return new(id, Guid.NewGuid(), "hash", DateTimeOffset.Parse("2026-10-01T10:00:00Z"), [], label)
            {
                GroupRunsOnly = false,
                Runs = 0,
                GroupRuns = 0,
            };
        }
        return new VersionDiff
        {
            From = Version(Archived, "old"),
            To = Version(Active, null),
            Added = changed ? 1 : 0,
            Removed = changed ? 1 : 0,
            Blocks = changed
                ?
                [
                    new DiffBlock
                    {
                        Folded = true,
                        Rows =
                        [
                            new DiffRow
                            {
                                Kind = DiffKind.Same,
                                Text = "a",
                                FromLine = 1,
                                ToLine = 1,
                            },
                            new DiffRow
                            {
                                Kind = DiffKind.Same,
                                Text = "b",
                                FromLine = 2,
                                ToLine = 2,
                            },
                        ],
                    },
                    new DiffBlock
                    {
                        Folded = false,
                        Rows =
                        [
                            new DiffRow
                            {
                                Kind = DiffKind.Same,
                                Text = "keep",
                                FromLine = 3,
                                ToLine = 3,
                            },
                            new DiffRow
                            {
                                Kind = DiffKind.Removed,
                                Text = "was",
                                FromLine = 4,
                            },
                            new DiffRow
                            {
                                Kind = DiffKind.Added,
                                Text = "is",
                                ToLine = 4,
                            },
                        ],
                    },
                ]
                : [],
        };
    }

    private static (int Exit, string Output, string Error, FakeApi Api, List<Uri> Opened) PrintDiff(bool changed, params string[] args)
    {
        var api = new FakeApi(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(SampleDiff(changed), ContractJson.Options),
                Encoding.UTF8,
                "application/json"
            ),
        });
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var opened = new List<Uri>();
        var exit = CliApp.Run(
            ["versions", "diff", Archived.ToString(), Active.ToString(), .. args, "--dir", Sample],
            new StringReader(""),
            output,
            error,
            http,
            opened.Add
        );
        return (exit, output.ToString(), error.ToString(), api, opened);
    }

    [Fact]
    public void Diff_by_default_prints_a_unified_style_diff_in_one_request()
    {
        var (exit, output, error, api, opened) = PrintDiff(true);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Empty(opened);
        Assert.Equal([$"/agents/example-agent/versions/diff"], api.Calls.Select(call => call.Path));
        var expected = string.Join(
            Environment.NewLine,
            $"--- version {Archived} (old)",
            $"+++ version {Active}",
            "… 2 unchanged lines",
            "@@",
            " keep",
            "-was",
            "+is",
            "+1 −1 lines"
        );
        Assert.Equal(expected, output.TrimEnd());
    }

    [Fact]
    public void Diff_without_changes_says_so()
    {
        var (exit, output, _, _, _) = PrintDiff(false);

        Assert.Equal(0, exit);
        Assert.EndsWith("No changes.", output.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_json_prints_the_record()
    {
        var (exit, output, _, _, opened) = PrintDiff(true, "--json");

        Assert.Equal(0, exit);
        Assert.Empty(opened);
        Assert.Equal(1, JsonSerializer.Deserialize<VersionDiff>(output, ContractJson.Options)?.Added);
    }

    private static HttpResponseMessage Preview() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new VersionDeletion(Archived, 3, 12, 36, "old"), ContractJson.Options),
                Encoding.UTF8,
                "application/json"
            ),
        };

    private static HttpResponseMessage DeleteAnswer(HttpRequestMessage request) =>
        request.Method == HttpMethod.Delete ? new HttpResponseMessage(HttpStatusCode.NoContent) : Preview();

    private static (int Exit, string Output, string Error, FakeApi Api) Delete(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        string typed,
        bool interactive,
        params string[] args
    )
    {
        var api = new FakeApi(answer);
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(
            ["versions", "delete", Archived.ToString(), .. args, "--dir", Sample],
            new StringReader(typed),
            output,
            error,
            http,
            interactive: interactive
        );
        return (exit, output.ToString(), error.ToString(), api);
    }

    [Fact]
    public void Delete_dry_run_prints_what_would_go_and_deletes_nothing()
    {
        var (exit, output, error, api) = Delete(DeleteAnswer, "", interactive: true, "--dry-run");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Contains("would delete: 3 run(s), 12 sample(s), 36 verdict(s)", output, StringComparison.Ordinal);
        var call = Assert.Single(api.Calls);
        Assert.Equal((HttpMethod.Get, $"/agents/example-agent/versions/{Archived}/deletion"), (call.Method, call.Path));
    }

    [Fact]
    public void Delete_with_yes_deletes_without_asking()
    {
        var (exit, output, _, api) = Delete(DeleteAnswer, "", interactive: false, "--yes");

        Assert.Equal(0, exit);
        Assert.Contains($"deleted version {Archived}", output, StringComparison.Ordinal);
        Assert.Equal(
            [
                (HttpMethod.Get, $"/agents/example-agent/versions/{Archived}/deletion"),
                (HttpMethod.Delete, $"/agents/example-agent/versions/{Archived}"),
            ],
            api.Calls.Select(call => (call.Method, call.Path))
        );
    }

    [Fact]
    public void Delete_without_a_terminal_and_without_yes_exits_1_and_deletes_nothing()
    {
        var (exit, _, error, api) = Delete(DeleteAnswer, $"{Archived}\n", interactive: false);

        Assert.Equal(1, exit);
        Assert.Contains("--yes", error, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, call => call.Method == HttpMethod.Delete);
    }

    [Fact]
    public void Delete_in_a_terminal_deletes_when_the_id_is_typed()
    {
        var (exit, output, _, api) = Delete(DeleteAnswer, $"{Archived}\n", interactive: true);

        Assert.Equal(0, exit);
        Assert.Contains("Type the version id to confirm", output, StringComparison.Ordinal);
        Assert.Contains(api.Calls, call => call.Method == HttpMethod.Delete);
    }

    [Theory]
    [InlineData("yes\n")]
    [InlineData("\n")]
    [InlineData("")]
    public void Delete_in_a_terminal_with_anything_but_the_id_exits_1_and_deletes_nothing(string typed)
    {
        var (exit, _, error, api) = Delete(DeleteAnswer, typed, interactive: true);

        Assert.Equal(1, exit);
        Assert.Contains("Not deleted", error, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, call => call.Method == HttpMethod.Delete);
    }

    [Fact]
    public void Delete_of_a_version_the_server_refuses_exits_2_with_its_message_and_deletes_nothing()
    {
        var (exit, _, error, api) = Delete(
            _ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    /*lang=json,strict*/
                    """{"title":"Version not archived","detail":"Only an archived version can be deleted: archive it first."}""",
                    Encoding.UTF8,
                    "application/problem+json"
                ),
            },
            "",
            interactive: true,
            "--yes"
        );

        Assert.Equal(2, exit);
        Assert.Contains("archive it first", error, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, call => call.Method == HttpMethod.Delete);
    }
}
