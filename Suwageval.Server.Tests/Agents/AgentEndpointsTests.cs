using System.Net;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Agents;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Agents;

public sealed class AgentEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private static AgentContract Version(string prompt = "Be helpful.") =>
        new()
        {
            Agents =
            [
                new LlmAgentDefinition
                {
                    Name = "main",
                    Model = new ModelDefinition { Name = "some-model" },
                    Config = prompt,
                },
            ],
        };

    /// <summary>What a run does when it starts: registers the contract the agent serves. The endpoint for it is gone.</summary>
    private async Task<AgentVersion> RegisterAsync(string agentName, AgentContract contract)
    {
        var store = new AgentStore(database.Pool);
        var agent =
            await store.GetAsync(agentName, TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("no agent");
        var row = (
            await store.RegisterVersionAsync(
                agent.Id,
                ContractContent.Json(contract),
                contract.Label,
                contract.Context.Retrieval?.Description,
                TestContext.Current.CancellationToken
            )
        ).Version;
        return new AgentVersion(
            row.Id,
            row.AgentId,
            row.Label,
            row.ContentHash,
            row.CreatedAt,
            row.ArchivedAt,
            ContractContent.ModelNames(contract),
            ContractContent.DataVersion(contract),
            contract.Context.Retrieval?.Description
        );
    }

    [Fact]
    public async Task Posting_an_agent_creates_it_then_returns_it_and_updates_the_endpoint()
    {
        var name = AgentStoreTests.UniqueName();

        var created = await server.Post("/agents", new AgentDefinition { Name = name, EndpointUrl = "http://one.test" });
        var same = await server.Post("/agents", new AgentDefinition { Name = name, EndpointUrl = "http://one.test" });
        var moved = await server.Post("/agents", new AgentDefinition { Name = name, EndpointUrl = "http://two.test" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var agent = await TestServer.Read<Agent>(created);
        Assert.Equal(agent.Id, (await TestServer.Read<Agent>(same)).Id);
        Assert.Equal("http://two.test", (await TestServer.Read<Agent>(moved)).EndpointUrl);
        Assert.Equal("http://two.test", (await TestServer.Read<AgentDetail>(await server.Get($"/agents/{name}"))).EndpointUrl);
    }

    [Theory]
    [InlineData("", "http://a.test", "name")]
    [InlineData("a", "not a url", "endpointUrl")]
    [InlineData("a", "ftp://a.test", "endpointUrl")]
    public async Task An_invalid_agent_is_a_422_naming_the_field(string name, string url, string field)
    {
        var response = await server.Post("/agents", new AgentDefinition { Name = name, EndpointUrl = url });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal([field], await TestServer.ErrorFields(response));
    }

    [Fact]
    public async Task A_body_with_an_unknown_field_is_a_400_problem()
    {
        var response = await server.PostRaw(
            "/agents", /*lang=json,strict*/
            """{"name":"a","endpointUrl":"http://a.test","colour":"red"}"""
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_list_has_the_agent_and_the_detail_counts_what_is_stored_by_name_or_id()
    {
        var name = await server.NewAgent();
        var id = (await TestServer.Read<AgentDetail>(await server.Get($"/agents/{name}"))).Id;
        await RegisterAsync(name, Version());

        var listed = await TestServer.Read<Agent[]>(await server.Get("/agents"));
        var byName = await TestServer.Read<AgentDetail>(await server.Get($"/agents/{name}"));
        var byId = await TestServer.Read<AgentDetail>(await server.Get($"/agents/{id}"));

        Assert.Contains(listed, agent => agent.Name == name);
        Assert.Equal(byName, byId);
        Assert.Equal((1, 0, 0), (byName.Versions, byName.Scenarios, byName.Checks));
    }

    [Fact]
    public async Task An_unknown_agent_is_a_404_problem_on_every_agent_route()
    {
        var response = await server.Get("/agents/no-such-agent");
        var versions = await server.Get("/agents/no-such-agent/versions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, versions.StatusCode);
    }

    [Fact]
    public async Task A_version_cannot_be_posted_because_it_comes_from_the_agent_when_a_run_starts()
    {
        var name = await server.NewAgent();

        var response = await server.Post($"/agents/{name}/versions", Version());

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task A_version_of_another_agent_or_a_made_up_id_is_a_404()
    {
        var one = await server.NewAgent();
        var two = await server.NewAgent();
        var version = await RegisterAsync(one, Version());

        var other = await server.Get($"/agents/{two}/versions/{version.Id}");
        var missing = await server.Get($"/agents/{one}/versions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Versions_are_listed_and_one_is_read_with_its_content()
    {
        var name = await server.NewAgent();
        var version = await RegisterAsync(
            name,
            Version() with
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
    public async Task Versions_are_archived_and_restored_by_id_and_stay_listed()
    {
        var name = await server.NewAgent();
        var one = await RegisterAsync(name, Version("one"));
        var two = await RegisterAsync(name, Version("two"));

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
        var mine = await RegisterAsync(name, Version());
        var theirs = await RegisterAsync(other, Version());

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
