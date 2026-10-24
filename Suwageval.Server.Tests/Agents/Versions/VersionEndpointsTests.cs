using System.Net;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Agents.Versions;

public sealed class VersionEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    public ValueTask DisposeAsync() => server.DisposeAsync();

    [Fact]
    public async Task A_version_cannot_be_posted_because_it_comes_from_the_agent_when_a_run_starts()
    {
        var name = await server.NewAgent();

        var response = await server.Post($"/agents/{name}/versions", AgentEndpointsTests.Version());

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task A_version_of_another_agent_or_a_made_up_id_is_a_404()
    {
        var one = await server.NewAgent();
        var two = await server.NewAgent();
        var version = await AgentEndpointsTests.RegisterAsync(database, one, AgentEndpointsTests.Version());

        var other = await server.Get($"/agents/{two}/versions/{version.Id}");
        var missing = await server.Get($"/agents/{one}/versions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("", "group-only,mixed,recheck,no-runs")]
    [InlineData("?grouping=All", "group-only,mixed,recheck,no-runs")]
    [InlineData("?grouping=None", "mixed,no-runs")]
    [InlineData("?grouping=Any", "group-only,recheck")]
    public async Task Versions_are_listed_all_by_default_and_by_grouping_none_or_any_with_the_group_runs_only_mark(
        string query,
        string expected
    )
    {
        ArgumentNullException.ThrowIfNull(expected);
        var (name, seeded) = await VersionStoreTests.SeedGroupRunsAsync(database);
        var models = new Dictionary<Guid, string>
        {
            [seeded.GroupOnly] = "group-only",
            [seeded.Mixed] = "mixed",
            [seeded.RecheckOfGroup] = "recheck",
            [seeded.NoRuns] = "no-runs",
        };

        var listed = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions{query}"));

        Assert.Equal(expected.Split(',').Order(), listed.Select(version => models[version.Id]).Order());
        Assert.All(
            listed,
            version => Assert.Equal(version.Id == seeded.GroupOnly || version.Id == seeded.RecheckOfGroup, version.GroupRunsOnly)
        );
        Assert.All(
            listed,
            version =>
                Assert.Equal(
                    version.Id switch
                    {
                        var id when id == seeded.GroupOnly => (0, 1),
                        var id when id == seeded.Mixed => (1, 1),
                        var id when id == seeded.RecheckOfGroup => (0, 2),
                        _ => (0, 0),
                    },
                    (version.Runs, version.GroupRuns)
                )
        );
    }

    [Fact]
    public async Task An_unknown_grouping_is_not_a_versions_list()
    {
        var name = await server.NewAgent();

        var response = await server.Get($"/agents/{name}/versions?grouping=Some");

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Versions_are_listed_and_one_is_read_with_its_content()
    {
        var name = await server.NewAgent();
        var version = await AgentEndpointsTests.RegisterAsync(
            database,
            name,
            AgentEndpointsTests.Version() with
            {
                Label = "v1",
                Context = new ContextDefinition
                {
                    UsesRetrieval = true,
                    Retrieval = new RetrievalDefinition { DataVersion = "build-7", Description = "1832 articles" },
                },
            }
        );

        var listed = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));
        var detail = await TestServer.Read<AgentVersionDetail>(await server.Get($"/agents/{name}/versions/{version.Id}"));

        Assert.Equal(version.Id, Assert.Single(listed).Id);
        Assert.Equal(["some-model"], listed[0].ModelNames);
        Assert.Equal("build-7", listed[0].DataVersion);
        Assert.Equal("1832 articles", listed[0].DataDescription);
        Assert.Equal("1832 articles", detail.Content.Context.Retrieval?.Description);
        Assert.Equal(version.Id, detail.Id);
        Assert.Equal("Be helpful.", detail.Content.Agents[0].Config);
        Assert.Equal("v1", detail.Content.Label);
    }

    [Fact]
    public async Task Two_versions_are_diffed_line_by_line_the_older_first_whatever_the_order_asked_for()
    {
        var name = await server.NewAgent();
        var older = await AgentEndpointsTests.RegisterAsync(database, name, AgentEndpointsTests.Version("Be helpful."));
        var newer = await AgentEndpointsTests.RegisterAsync(database, name, AgentEndpointsTests.Version("Be brief.\nBe kind."));

        var diff = await TestServer.Read<VersionDiff>(await server.Get($"/agents/{name}/versions/diff?from={newer.Id}&to={older.Id}"));

        Assert.Equal((older.Id, newer.Id), (diff.From.Id, diff.To.Id));
        Assert.Equal((3, 1), (diff.Added, diff.Removed));
        var rows = diff.Blocks.SelectMany(block => block.Rows).ToList();
        Assert.Contains(rows, row => row is { Kind: DiffKind.Removed, Text: "  config: Be helpful." });
        Assert.Contains(rows, row => row is { Kind: DiffKind.Added, Text: "    Be brief." });
        Assert.Contains(rows, row => row is { Kind: DiffKind.Same, Text: "- name: main", FromLine: 2, ToLine: 2 });
    }

    [Fact]
    public async Task Diffing_refuses_what_cannot_be_compared()
    {
        var name = await server.NewAgent();
        var other = await server.NewAgent();
        var version = await AgentEndpointsTests.RegisterAsync(database, name, AgentEndpointsTests.Version());
        var foreign = await AgentEndpointsTests.RegisterAsync(database, other, AgentEndpointsTests.Version("another"));
        var unknown = Guid.NewGuid();

        var itself = await server.Get($"/agents/{name}/versions/diff?from={version.Id}&to={version.Id}");
        var missing = await server.Get($"/agents/{name}/versions/diff?from={version.Id}");
        var ofAnother = await server.Get($"/agents/{name}/versions/diff?from={version.Id}&to={foreign.Id}");
        var noVersion = await server.Get($"/agents/{name}/versions/diff?from={version.Id}&to={unknown}");
        var noAgent = await server.Get($"/agents/{Guid.NewGuid()}/versions/diff?from={version.Id}&to={foreign.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, itself.StatusCode);
        Assert.Equal(["to"], await TestServer.ErrorFields(missing));
        Assert.Equal(["to"], await TestServer.ErrorFields(ofAnother));
        Assert.Equal(HttpStatusCode.NotFound, noVersion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noAgent.StatusCode);
    }

    [Fact]
    public async Task Versions_are_archived_and_restored_by_id_and_stay_listed()
    {
        var name = await server.NewAgent();
        var one = await AgentEndpointsTests.RegisterAsync(database, name, AgentEndpointsTests.Version("one"));
        var two = await AgentEndpointsTests.RegisterAsync(database, name, AgentEndpointsTests.Version("two"));

        var archived = await server.Post($"/agents/{name}/versions/archive", new ArchiveVersionsRequest([one.Id], Archived: true));
        var afterArchive = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));
        var detail = await TestServer.Read<AgentVersionDetail>(await server.Get($"/agents/{name}/versions/{one.Id}"));
        var restored = await server.Post($"/agents/{name}/versions/archive", new ArchiveVersionsRequest([one.Id], Archived: false));
        var afterRestore = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));

        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        Assert.NotNull(afterArchive.Single(version => version.Id == one.Id).ArchivedAt);
        Assert.Null(afterArchive.Single(version => version.Id == two.Id).ArchivedAt);
        Assert.NotNull(detail.ArchivedAt);
        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        Assert.All(afterRestore, version => Assert.Null(version.ArchivedAt));
    }

    [Fact]
    public async Task Archiving_with_an_id_that_is_not_a_version_of_the_agent_is_a_404_and_changes_nothing()
    {
        var name = await server.NewAgent();
        var other = await server.NewAgent();
        var mine = await AgentEndpointsTests.RegisterAsync(database, name, AgentEndpointsTests.Version());
        var theirs = await AgentEndpointsTests.RegisterAsync(database, other, AgentEndpointsTests.Version());

        var foreign = await server.Post($"/agents/{name}/versions/archive", new ArchiveVersionsRequest([mine.Id, theirs.Id], true));
        var noAgent = await server.Post("/agents/no-such-agent/versions/archive", new ArchiveVersionsRequest([mine.Id], true));

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal("application/problem+json", foreign.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            theirs.Id.ToString(),
            await foreign.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal
        );
        Assert.Equal(HttpStatusCode.NotFound, noAgent.StatusCode);
        var listed = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));
        Assert.All(listed, version => Assert.Null(version.ArchivedAt));
    }
}
