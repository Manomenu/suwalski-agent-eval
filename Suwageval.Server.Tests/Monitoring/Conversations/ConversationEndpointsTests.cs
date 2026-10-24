using System.Net;
using System.Text.Json;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Monitoring;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Monitoring;
using Suwageval.Server.Agents;
using Suwageval.Server.Monitoring.Conversations;
using Suwageval.Server.Tests.Agents;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Monitoring.Conversations;

public sealed class ConversationEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private static AgentContract Contract(string config = "Be helpful.") => AgentEndpointsTests.Version(config);

    private async Task<string> MonitoredAgent(MonitoringSettings? monitoring = null)
    {
        var name = AgentStoreTests.UniqueName();
        var response = await server.Post(
            "/agents",
            new AgentDefinition
            {
                Name = name,
                EndpointUrl = "http://a.test",
                Features = new AgentFeatures { Monitoring = monitoring ?? new MonitoringSettings() },
            }
        );
        response.EnsureSuccessStatusCode();
        return name;
    }

    private static MonitoredTurn Turn(string conversation, string text, DateTimeOffset at, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ConversationId = conversation,
            At = at,
            Items = [new UserMessage { Text = text }, new AssistantMessage { Text = $"About: {text}" }],
        };

    private static TurnBatch Batch(string agent, AgentContract contract, bool withContract, params MonitoredTurn[] turns) =>
        new()
        {
            Agent = agent,
            Environment = "production",
            ContractHash = AgentContractHash.Of(contract),
            Contract = withContract ? contract : null,
            Turns = turns,
        };

    private async Task<TurnBatchAnswer> Send(TurnBatch batch)
    {
        var response = await server.Post("/monitoring/turns", batch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TestServer.Read<TurnBatchAnswer>(response);
    }

    private static readonly DateTimeOffset Noon = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_unknown_contract_is_asked_for_and_with_it_the_version_is_registered_and_the_turns_stored()
    {
        var name = await MonitoredAgent();
        var turn = Turn("c-1", "Where is my order?", Noon);

        var first = await Send(Batch(name, Contract(), withContract: false, turn));
        var second = await Send(Batch(name, Contract(), withContract: true, turn));
        var third = await Send(Batch(name, Contract(), withContract: false, Turn("c-1", "And now?", Noon.AddMinutes(1))));

        Assert.Equal((0, true), (first.Stored, first.SendContract));
        Assert.Equal((1, false), (second.Stored, second.SendContract));
        Assert.Equal((1, false), (third.Stored, third.SendContract));
        var list = await TestServer.Read<ConversationList>(await server.Get($"/monitoring/agents/{name}/conversations"));
        var conversation = Assert.Single(list.Conversations);
        Assert.Equal(
            ("c-1", "production", ConversationState.Open, 2),
            (conversation.ConversationId, conversation.Environment, conversation.State, conversation.Turns)
        );
        Assert.Equal(["production"], list.Environments);
        var versions = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));
        Assert.Equal(AgentContractHash.Of(Contract()), Assert.Single(versions).ContentHash);
        Assert.Equal(conversation.AgentVersionId, versions[0].Id);
    }

    [Fact]
    public async Task A_turn_sent_twice_is_stored_once_and_the_detail_lists_the_turns_oldest_first_with_their_items()
    {
        var name = await MonitoredAgent();
        var later = Turn("c-2", "Second", Noon.AddMinutes(5));
        var earlier = Turn("c-2", "First", Noon);
        await Send(Batch(name, Contract(), withContract: true, later, earlier));

        var again = await Send(Batch(name, Contract(), withContract: false, later));

        Assert.Equal(0, again.Stored);
        var id = (await TestServer.Read<ConversationList>(await server.Get($"/monitoring/agents/{name}/conversations")))
            .Conversations[0]
            .Id;
        var detail = await TestServer.Read<ConversationDetail>(await server.Get($"/monitoring/conversations/{id}"));
        Assert.Equal(2, detail.Conversation.Turns);
        Assert.Equal(["First", "Second"], detail.Turns.Select(turn => Assert.IsType<UserMessage>(turn.Items[0]).Text));
        Assert.Equal("About: First", Assert.IsType<AssistantMessage>(detail.Turns[0].Items[1]).Text);
        Assert.Equal(Noon, detail.Conversation.StartedAt);
        Assert.Equal(Noon.AddMinutes(5), detail.Conversation.LastTurnAt);
    }

    [Fact]
    public async Task A_redeploy_during_a_conversation_moves_it_to_the_new_version_and_marks_the_change()
    {
        var name = await MonitoredAgent();
        await Send(Batch(name, Contract(), withContract: true, Turn("c-3", "Before", Noon)));

        await Send(Batch(name, Contract("Be brief."), withContract: true, Turn("c-3", "After", Noon.AddMinutes(1))));

        var conversation = (
            await TestServer.Read<ConversationList>(await server.Get($"/monitoring/agents/{name}/conversations"))
        ).Conversations[0];
        Assert.True(conversation.VersionChanged);
        var versions = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));
        Assert.Equal(
            versions.Single(version => version.ContentHash == AgentContractHash.Of(Contract("Be brief."))).Id,
            conversation.AgentVersionId
        );
    }

    [Fact]
    public async Task The_versions_list_says_where_each_version_answered_and_which_answers_now()
    {
        var name = await MonitoredAgent();
        var now = DateTimeOffset.UtcNow;
        await Send(
            Batch(name, Contract(), withContract: true, Turn("old", "Long ago", now.AddDays(-40)), Turn("c-1", "Hi", now.AddHours(-2)))
        );
        await Send(Batch(name, Contract("Be brief."), withContract: true, Turn("c-2", "Hi", now.AddHours(-1))));

        var versions = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));

        var first = versions.Single(version => version.ContentHash == AgentContractHash.Of(Contract()));
        var second = versions.Single(version => version.ContentHash == AgentContractHash.Of(Contract("Be brief.")));
        var before = Assert.Single(first.Monitoring);
        // The conversation of 40 days ago is out of the 30 days' count, but the version answered it.
        Assert.Equal(("production", 1, false), (before.Environment, before.Conversations30Days, before.Current));
        Assert.Equal(now.AddHours(-2), before.LastConversationAt, TimeSpan.FromSeconds(1));
        var current = Assert.Single(second.Monitoring);
        Assert.Equal((1, true), (current.Conversations30Days, current.Current));
    }

    [Fact]
    public async Task With_monitoring_off_the_turns_are_refused_409_naming_the_feature()
    {
        var name = AgentStoreTests.UniqueName();
        await server.Post("/agents", new AgentDefinition { Name = name, EndpointUrl = "http://a.test" });

        var response = await server.Post("/monitoring/turns", Batch(name, Contract(), withContract: true, Turn("c", "Hi", Noon)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("monitoring", (await TestServer.Read<JsonElement>(response)).GetProperty("feature").GetString());
    }

    [Fact]
    public async Task An_environment_the_agent_does_not_take_is_409_and_a_malformed_one_422()
    {
        var name = await MonitoredAgent(new MonitoringSettings { Environments = ["production"] });

        var staging = await server.Post(
            "/monitoring/turns",
            Batch(name, Contract(), withContract: true, Turn("c", "Hi", Noon)) with
            {
                Environment = "staging",
            }
        );
        var malformed = await server.Post(
            "/monitoring/turns",
            Batch(name, Contract(), withContract: true, Turn("c", "Hi", Noon)) with
            {
                Environment = "Staging!",
            }
        );

        Assert.Equal(HttpStatusCode.Conflict, staging.StatusCode);
        Assert.Contains(
            "features.monitoring.environments",
            (await TestServer.Read<JsonElement>(staging)).GetProperty("detail").GetString(),
            StringComparison.Ordinal
        );
        Assert.Equal(["environment"], await TestServer.ErrorFields(malformed));
    }

    [Fact]
    public async Task A_contract_that_is_not_the_one_hashed_and_a_turn_without_items_are_422()
    {
        var name = await MonitoredAgent();

        var mismatch = await server.Post(
            "/monitoring/turns",
            Batch(name, Contract(), withContract: true, Turn("c", "Hi", Noon)) with
            {
                Contract = Contract("Other."),
            }
        );
        var empty = await server.Post(
            "/monitoring/turns",
            Batch(name, Contract(), withContract: true, Turn("c", "Hi", Noon) with { Items = [] })
        );

        Assert.Equal(["contractHash"], await TestServer.ErrorFields(mismatch));
        Assert.Equal(["turns[0].items"], await TestServer.ErrorFields(empty));
    }

    [Fact]
    public async Task An_idle_conversation_is_closed_after_close_after_and_a_new_turn_opens_it_again()
    {
        var name = await MonitoredAgent(new MonitoringSettings { CloseAfter = "1m" });
        await Send(Batch(name, Contract(), withContract: true, Turn("c-4", "Hi", Noon)));
        var store = new ConversationStore(database.Pool);
        var agent = (await new AgentStore(database.Pool).GetAsync(name, TestContext.Current.CancellationToken))!;

        var notYet = await store.CloseIdleAsync(agent.Id, DateTimeOffset.UtcNow.AddMinutes(-1), TestContext.Current.CancellationToken);
        var closed = await store.CloseIdleAsync(agent.Id, DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);
        var whenClosed = (
            await TestServer.Read<ConversationList>(await server.Get($"/monitoring/agents/{name}/conversations?state=Closed"))
        ).Conversations;
        await Send(Batch(name, Contract(), withContract: false, Turn("c-4", "Still there?", Noon.AddHours(1))));
        var reopened = (
            await TestServer.Read<ConversationList>(await server.Get($"/monitoring/agents/{name}/conversations"))
        ).Conversations;

        Assert.Equal((0, 1), (notYet, closed));
        Assert.Equal(ConversationState.Closed, Assert.Single(whenClosed).State);
        Assert.Equal((ConversationState.Open, 2), (Assert.Single(reopened).State, reopened[0].Turns));
    }

    [Fact]
    public async Task The_list_filters_by_environment_and_pages_and_an_unknown_conversation_is_404()
    {
        var name = await MonitoredAgent();
        await Send(Batch(name, Contract(), withContract: true, Turn("p-1", "a", Noon), Turn("p-2", "b", Noon.AddMinutes(1))));
        await Send(Batch(name, Contract(), withContract: false, Turn("s-1", "c", Noon)) with { Environment = "staging" });

        var production = await TestServer.Read<ConversationList>(
            await server.Get($"/monitoring/agents/{name}/conversations?environment=production&pageSize=1")
        );
        var missing = await server.Get($"/monitoring/conversations/{Guid.NewGuid()}");

        Assert.Equal((2, 1), (production.TotalCount, production.Conversations.Count));
        Assert.Equal("p-2", production.Conversations[0].ConversationId);
        Assert.Equal(["production", "staging"], production.Environments);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_version_takes_the_conversations_it_answered_a_turn_of_whole_and_the_preview_counts_them()
    {
        var name = await MonitoredAgent();
        await Send(Batch(name, Contract(), withContract: true, Turn("old-only", "a", Noon), Turn("both", "b", Noon)));
        await Send(
            Batch(name, Contract("Be brief."), withContract: true, Turn("both", "c", Noon.AddMinutes(1)), Turn("new-only", "d", Noon))
        );
        var versions = await TestServer.Read<AgentVersion[]>(await server.Get($"/agents/{name}/versions"));
        var old = versions.Single(version => version.ContentHash == AgentContractHash.Of(Contract())).Id;
        await server.Post($"/agents/{name}/versions/archive", new ArchiveVersionsRequest([old], Archived: true));

        var preview = await TestServer.Read<VersionDeletion>(await server.Get($"/agents/{name}/versions/{old}/deletion"));
        var deleted = await server.Delete($"/agents/{name}/versions/{old}");

        Assert.Equal(2, preview.Conversations);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var left = (await TestServer.Read<ConversationList>(await server.Get($"/monitoring/agents/{name}/conversations"))).Conversations;
        Assert.Equal(["new-only"], left.Select(conversation => conversation.ConversationId));
    }
}
