using System.Net;
using System.Text.Json;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Agents;

public sealed class AgentEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private static AgentVersionInput Version(string prompt = "Be helpful.") =>
        new()
        {
            SystemPrompt = prompt,
            Model = new ModelDefinition { Name = "some-model" },
        };

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
        await server.Post($"/agents/{name}/versions", Version());

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
        var registered = await server.Post("/agents/no-such-agent/versions", Version());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, versions.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, registered.StatusCode);
    }

    [Fact]
    public async Task A_version_is_created_once_per_content_and_a_label_alone_is_not_new_content()
    {
        var name = await server.NewAgent();

        var first = await server.Post($"/agents/{name}/versions", Version() with { Label = "v1" });
        var again = await server.Post($"/agents/{name}/versions", Version() with { Label = "renamed" });
        var changed = await server.Post($"/agents/{name}/versions", Version("Be brief."));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.Created, changed.StatusCode);
        var firstVersion = await TestServer.Read<AgentVersion>(first);
        Assert.Equal(firstVersion, await TestServer.Read<AgentVersion>(again));
        Assert.Equal("v1", firstVersion.Label);
        Assert.NotEqual(firstVersion.Id, (await TestServer.Read<AgentVersion>(changed)).Id);
    }

    [Fact]
    public async Task Versions_are_listed_and_one_is_read_with_its_content()
    {
        var name = await server.NewAgent();
        var version = await TestServer.Read<AgentVersion>(await server.Post($"/agents/{name}/versions", Version() with { Label = "v1" }));

        var listed = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));
        var detail = await TestServer.Read<AgentVersionDetail>(await server.Get($"/agents/{name}/versions/{version.Id}"));

        Assert.Equal([version], listed);
        Assert.Equal(version.Id, detail.Id);
        Assert.Equal("Be helpful.", detail.Content.SystemPrompt);
        Assert.Equal("v1", detail.Content.Label);
    }

    [Fact]
    public async Task A_version_of_another_agent_or_a_made_up_id_is_a_404()
    {
        var one = await server.NewAgent();
        var two = await server.NewAgent();
        var version = await TestServer.Read<AgentVersion>(await server.Post($"/agents/{one}/versions", Version()));

        var other = await server.Get($"/agents/{two}/versions/{version.Id}");
        var missing = await server.Get($"/agents/{one}/versions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task An_invalid_version_is_a_422_naming_the_fields()
    {
        var name = await server.NewAgent();
        var tool = new ToolDefinition
        {
            Name = "t",
            Description = "d",
            Parameters = JsonDocument.Parse("{}").RootElement,
        };

        var response = await server.Post(
            $"/agents/{name}/versions",
            new AgentVersionInput
            {
                SystemPrompt = " ",
                Model = new ModelDefinition { Name = "" },
                Tools = [tool, tool],
            }
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["systemPrompt", "model.name", "tools[1].name"], await TestServer.ErrorFields(response));
    }
}
