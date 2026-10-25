using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.AgentSdk.Monitoring;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Monitoring;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Monitoring.Checking;
using Suwageval.Server.Monitoring.Conversations;
using Suwageval.Server.Tests.Agents;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests.Monitoring.Checking;

/// <summary>The agent's monitoring checks: pushed whole, run on each closed conversation, counted per check and per day.</summary>
public sealed class MonitoringChecksTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    private static readonly CheckInput NoRefund = new ToolNotCalledCheck
    {
        Key = "no-refund",
        Description = "Never refunds on its own",
        Tool = "refund_order",
    };

    private static readonly CheckInput SaysHello = new TextCheck
    {
        Key = "says-hello",
        Description = "Greets the customer",
        Contains = "Hello",
    };

    private static async Task<string> MonitoredAgent(HttpClient client)
    {
        var name = AgentStoreTests.UniqueName();
        var response = await client.PostAsJsonAsync(
            new Uri("/agents", UriKind.Relative),
            new AgentDefinition
            {
                Name = name,
                EndpointUrl = "http://a.test",
                Features = new AgentFeatures { Monitoring = new MonitoringSettings() },
            },
            ContractJson.Options,
            Cancellation
        );
        response.EnsureSuccessStatusCode();
        return name;
    }

    private static Task<HttpResponseMessage> PutChecks(HttpClient client, string agent, params CheckInput[] checks) =>
        client.PutAsJsonAsync(
            new Uri($"/monitoring/agents/{agent}/checks", UriKind.Relative),
            new PutChecksRequest(checks),
            ContractJson.Options,
            Cancellation
        );

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, Cancellation))!;
    }

    private static Task<T> Get<T>(HttpClient client, string url) =>
        client.GetFromJsonAsync<T>(new Uri(url, UriKind.Relative), ContractJson.Options, Cancellation)!;

    /// <summary>A conversation of one turn in which the agent answers <paramref name="answer"/>, after calling <paramref name="tool"/> when given.</summary>
    private static async Task SendAsync(HttpClient client, string agent, string conversation, string answer, string? tool = null)
    {
        var contract = AgentEndpointsTests.Version();
        List<ConversationItem> items = [new UserMessage { Text = "Hi" }];
        if (tool is not null)
        {
            items.Add(
                new ToolCall
                {
                    Id = "c1",
                    Name = tool,
                    Arguments = JsonSerializer.SerializeToElement(new { }),
                }
            );
            items.Add(new ToolResult { CallId = "c1", Content = "{}" });
        }
        items.Add(new AssistantMessage { Text = answer });
        var response = await client.PostAsJsonAsync(
            new Uri("/monitoring/turns", UriKind.Relative),
            new TurnBatch
            {
                Agent = agent,
                Environment = "production",
                ContractHash = AgentContractHash.Of(contract),
                Contract = contract,
                Turns =
                [
                    new MonitoredTurn
                    {
                        Id = Guid.NewGuid(),
                        ConversationId = conversation,
                        At = DateTimeOffset.UtcNow,
                        Items = items,
                    },
                ],
            },
            ContractJson.Options,
            Cancellation
        );
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Closes the agent's conversations at once and runs the checks on them, as the closer and the worker do in time.</summary>
    private async Task CloseAndCheckAsync(string agent)
    {
        var agentId = (await new AgentStore(database.Pool).GetAsync(agent, Cancellation))!.Id;
        await new ConversationStore(database.Pool).CloseIdleAsync(agentId, DateTimeOffset.UtcNow.AddMinutes(1), Cancellation);
        var checking = app.Services.GetRequiredService<CheckingStore>();
        var checker = app.Services.GetRequiredService<ConversationChecker>();
        foreach (var (conversationId, ofAgent) in await checking.UncheckedAsync(1000, Cancellation))
        {
            if (ofAgent == agentId)
            {
                Assert.True(await checker.CheckAsync(conversationId, ofAgent, Cancellation));
            }
        }
    }

    [Fact]
    public async Task A_push_replaces_the_checks_whole_with_a_revision_per_change()
    {
        var client = Client();
        var agent = await MonitoredAgent(client);

        var first = await Read<PutChecksResponse>(await PutChecks(client, agent, NoRefund, SaysHello));
        var second = await Read<PutChecksResponse>(
            await PutChecks(client, agent, NoRefund, SaysHello with { Description = "Says hello first" })
        );
        var third = await Read<PutChecksResponse>(await PutChecks(client, agent, NoRefund));

        Assert.Equal([UpsertOutcome.Created, UpsertOutcome.Created], first.Results.Select(result => result.Outcome));
        Assert.Equal([UpsertOutcome.Unchanged, UpsertOutcome.Updated], second.Results.Select(result => result.Outcome));
        Assert.Equal(
            [("no-refund", UpsertOutcome.Unchanged), ("says-hello", UpsertOutcome.Deleted)],
            third.Results.Select(result => (result.Key, result.Outcome))
        );
        var listed = await Get<MonitoringCheck[]>(client, $"/monitoring/agents/{agent}/checks");
        Assert.Equal(
            ("no-refund", 1, "Never refunds on its own"),
            (listed.Single().Check.Key, listed[0].Revision, listed[0].Check.Description)
        );
    }

    [Fact]
    public async Task Checks_that_break_the_rules_are_422_and_with_monitoring_off_409()
    {
        var client = Client();
        var agent = await MonitoredAgent(client);
        var off = AgentStoreTests.UniqueName();
        await client.PostAsJsonAsync(
            new Uri("/agents", UriKind.Relative),
            new AgentDefinition { Name = off, EndpointUrl = "http://a.test" },
            ContractJson.Options,
            Cancellation
        );

        var invalid = await PutChecks(
            client,
            agent,
            NoRefund with
            {
                Description = null,
            },
            SaysHello with
            {
                ScenarioKey = "greets",
                Samples = 3,
            },
            SaysHello with
            {
                PassThreshold = 0.5,
            }
        );
        var refused = await PutChecks(client, off, NoRefund);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var fields = (await invalid.Content.ReadFromJsonAsync<JsonElement>(Cancellation))
            .GetProperty("errors")
            .EnumerateObject()
            .Select(field => field.Name)
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            ["checks[0].description", "checks[1].samples", "checks[1].scenarioKey", "checks[2].key", "checks[2].passThreshold"],
            fields
        );
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    }

    [Fact]
    public async Task A_closed_conversation_gets_a_verdict_per_check_shown_counted_filtered_and_drawn()
    {
        var client = Client();
        var agent = await MonitoredAgent(client);
        await PutChecks(
            client,
            agent,
            NoRefund,
            SaysHello,
            new JudgeCheck
            {
                Key = "is-polite",
                Description = "Polite",
                Criteria = "Polite.",
            }
        );
        await SendAsync(client, agent, "good", "Hello! How can I help?");
        await SendAsync(client, agent, "bad", "Refunded.", tool: "refund_order");

        await CloseAndCheckAsync(agent);

        var list = await Get<ConversationList>(client, $"/monitoring/agents/{agent}/conversations");
        var bad = list.Conversations.Single(conversation => conversation.ConversationId == "bad");
        var good = list.Conversations.Single(conversation => conversation.ConversationId == "good");
        Assert.NotNull(bad.CheckedAt);
        // The judge check is not judged in the tests (no model): no verdict on the agent, so not counted as failed.
        Assert.Equal((2, 0), (bad.FailedChecks, good.FailedChecks));
        Assert.Equal((1, 1), (bad.NotJudgedChecks, good.NotJudgedChecks));
        var detail = await Get<ConversationDetail>(client, $"/monitoring/conversations/{bad.Id}");
        Assert.Equal(["is-polite", "no-refund", "says-hello"], detail.Checks.Select(check => check.Key));
        Assert.True(detail.Checks.Single(check => check.Key == "is-polite").NotJudged);
        Assert.Equal(
            "refund_order",
            Assert.IsType<ToolCall>(Assert.Single(detail.Checks.Single(check => check.Key == "no-refund").Evidence)).Name
        );
        var failing = await Get<ConversationList>(client, $"/monitoring/agents/{agent}/conversations?failedCheck=no-refund");
        Assert.Equal(["bad"], failing.Conversations.Select(conversation => conversation.ConversationId));
        var checks = await Get<MonitoringCheck[]>(client, $"/monitoring/agents/{agent}/checks");
        Assert.Equal(new CheckRate(1, 2), checks.Single(check => check.Check.Key == "no-refund").Last7Days);
        Assert.Equal(new CheckRate(0, 0), checks.Single(check => check.Check.Key == "is-polite").Last30Days);
        var trend = await Get<MonitoringTrend>(client, $"/monitoring/agents/{agent}/trend?days=7");
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(new MonitoringDay(today, 2, 2), Assert.Single(trend.Days));
        Assert.Equal(
            (1, 2),
            trend.Points.Where(point => point.CheckKey == "no-refund").Select(point => (point.Passed, point.Checked)).Single()
        );
        Assert.Single(trend.Versions);
        // The scenario drafted from it carries the checks it failed as comments (not the one not judged), and refuses a message it lacks.
        var draft = await Get<ScenarioDraft>(client, $"/monitoring/conversations/{bad.Id}/scenario?upToUserMessage=1");
        Assert.Contains("#   tool: refund_order\n", draft.Yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("is-polite", draft.Yaml, StringComparison.Ordinal);
        var missing = await client.GetAsync(
            new Uri($"/monitoring/conversations/{bad.Id}/scenario?upToAgentMessage=2", UriKind.Relative),
            Cancellation
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
    }

    [Fact]
    public async Task A_turn_after_the_close_takes_the_check_back_and_a_late_verdict_is_not_stored()
    {
        var client = Client();
        var agent = await MonitoredAgent(client);
        await PutChecks(client, agent, SaysHello);
        await SendAsync(client, agent, "again", "Hello!");
        await CloseAndCheckAsync(agent);
        var id = (await Get<ConversationList>(client, $"/monitoring/agents/{agent}/conversations")).Conversations[0].Id;

        await SendAsync(client, agent, "again", "Bye.");
        var reopened = (await Get<ConversationList>(client, $"/monitoring/agents/{agent}/conversations")).Conversations[0];
        var stored = await app
            .Services.GetRequiredService<ConversationChecker>()
            .CheckAsync(id, (await new AgentStore(database.Pool).GetAsync(agent, Cancellation))!.Id, Cancellation);

        Assert.Equal(ConversationState.Open, reopened.State);
        Assert.Null(reopened.CheckedAt);
        Assert.False(stored);
    }
}
