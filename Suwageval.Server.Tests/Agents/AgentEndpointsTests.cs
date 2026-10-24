using System.Net;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Agents;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Agents;

public sealed class AgentEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    public ValueTask DisposeAsync() => server.DisposeAsync();

    internal static AgentContract Version(string prompt = "Be helpful.") =>
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
    internal static async Task<AgentVersion> RegisterAsync(TestDatabase database, string agentName, AgentContract contract)
    {
        var store = new AgentStore(database.Pool);
        var agent =
            await store.GetAsync(agentName, TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("no agent");
        var row = (
            await store.RegisterVersionAsync(
                agent.Id,
                AgentContractHash.Content(contract),
                contract.Label,
                contract.Context.Retrieval?.Description,
                TestContext.Current.CancellationToken
            )
        ).Version;
        return new AgentVersion(
            row.Id,
            row.AgentId,
            row.ContentHash,
            row.CreatedAt,
            ContractContent.ModelNames(contract),
            row.Label,
            row.ArchivedAt,
            contract.Context.Retrieval?.DataVersion,
            contract.Context.Retrieval?.Description
        )
        {
            GroupRunsOnly = false,
            Runs = 0,
            GroupRuns = 0,
        };
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
    public async Task The_regression_settings_are_stored_replaced_and_put_back_to_the_defaults_when_left_out()
    {
        var name = AgentStoreTests.UniqueName();
        var store = new AgentStore(database.Pool);
        var settings = new RegressionSettings { Branches = ["master", "release"], History = 3 };

        await server.Post(
            "/agents",
            new AgentDefinition
            {
                Name = name,
                EndpointUrl = "http://a.test",
                Regression = settings,
            }
        );
        var stored = (await store.GetAsync(name, TestContext.Current.CancellationToken))!.Regression;
        var detail = await TestServer.Read<AgentDetail>(await server.Get($"/agents/{name}"));
        await server.Post(
            "/agents",
            new AgentDefinition
            {
                Name = name,
                EndpointUrl = "http://a.test",
                Regression = new RegressionSettings { History = 7 },
            }
        );
        var changed = (await store.GetAsync(name, TestContext.Current.CancellationToken))!.Regression;
        await server.Post("/agents", new AgentDefinition { Name = name, EndpointUrl = "http://a.test" });
        var cleared = (await store.GetAsync(name, TestContext.Current.CancellationToken))!.Regression;

        Assert.Equal(["master", "release"], stored!.Branches);
        Assert.Equal(3, detail.Regression!.History);
        Assert.Equal(["master", "release"], detail.Regression.Branches);
        Assert.Equal(3, stored.History);
        Assert.Null(changed!.Branches);
        Assert.Equal(7, changed.History);
        Assert.Null(cleared);
        Assert.Null((await TestServer.Read<AgentDetail>(await server.Get($"/agents/{name}"))).Regression);
    }

    [Fact]
    public async Task The_default_samples_are_stored_shown_and_put_back_when_left_out()
    {
        var name = AgentStoreTests.UniqueName();

        await server.Post(
            "/agents",
            new AgentDefinition
            {
                Name = name,
                EndpointUrl = "http://a.test",
                Samples = 5,
            }
        );
        var set = await TestServer.Read<AgentDetail>(await server.Get($"/agents/{name}"));
        await server.Post("/agents", new AgentDefinition { Name = name, EndpointUrl = "http://a.test" });
        var cleared = await TestServer.Read<AgentDetail>(await server.Get($"/agents/{name}"));

        Assert.Equal(5, set.Samples);
        Assert.Null(cleared.Samples);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Samples_out_of_range_are_a_422_naming_the_field(int samples)
    {
        var response = await server.Post(
            "/agents",
            new AgentDefinition
            {
                Name = AgentStoreTests.UniqueName(),
                EndpointUrl = "http://a.test",
                Samples = samples,
            }
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["samples"], await TestServer.ErrorFields(response));
    }

    [Fact]
    public async Task Invalid_regression_settings_are_a_422_naming_the_fields()
    {
        var response = await server.Post(
            "/agents",
            new AgentDefinition
            {
                Name = AgentStoreTests.UniqueName(),
                EndpointUrl = "http://a.test",
                Regression = new RegressionSettings { Branches = ["master", "master", "a b"], History = 0 },
            }
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            ["regression.branches[1]", "regression.branches[2]", "regression.history"],
            (await TestServer.ErrorFields(response)).Order(StringComparer.Ordinal)
        );
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
        await RegisterAsync(database, name, Version());

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
}
