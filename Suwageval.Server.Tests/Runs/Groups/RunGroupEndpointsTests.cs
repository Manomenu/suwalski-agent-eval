using System.Net;
using Dapper;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Checks;
using Suwageval.Server.Runs;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Tests.Api;

namespace Suwageval.Server.Tests.Runs.Groups;

/// <summary>Archiving, restoring and deleting run groups, and listing runs by whether they are in a group — on a real PostgreSQL.</summary>
public sealed class RunGroupEndpointsTests(TestDatabase database) : IAsyncDisposable
{
    private readonly TestServer server = new(database);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => server.DisposeAsync();

    private Task<SeededAgent> SeedAsync() =>
        SeededAgent.CreateAsync(
            database.Pool,
            "http://agent.test",
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

    /// <summary>A finished run with <paramref name="samples"/> samples, each with one verdict; a re-check when <paramref name="recheckOf"/> is given.</summary>
    private async Task<Guid> FinishedRunAsync(SeededAgent agent, string? group, int samples = 1, Guid? recheckOf = null)
    {
        var store = new RunStore(database.Pool);
        var run = await store.EnqueueAsync(
            agent.Id,
            agent.VersionId,
            [],
            agent.EndpointUrl,
            recheckOf,
            recheckOf,
            runGroup: group,
            cancellationToken: Cancellation
        );
        var scenario = await new ScenarioStore(database.Pool).GetAsync(agent.Id, "refund", Cancellation);
        var check = Assert.Single(await new CheckStore(database.Pool).ListAsync(agent.Id, cancellationToken: Cancellation));
        for (var sample = 1; sample <= samples; sample++)
        {
            await store.AddSampleAsync(
                run,
                new NewSample(
                    scenario!.Id,
                    scenario.Revision,
                    sample,
                    SampleStatus.Done,
                    /*lang=json,strict*/
                    """[{"kind":"AssistantMessage","text":"hi"}]""",
                    null,
                    10,
                    [],
                    [new NewCheckResult(check.Id, check.Revision, true, "[]", "ok")]
                ),
                Cancellation
            );
        }
        await store.FinishAsync(run, RunStatus.Done, cancellationToken: Cancellation);
        return run;
    }

    private async Task<List<RunGroupSummary>> GroupsAsync(SeededAgent agent) =>
        await TestServer.Read<List<RunGroupSummary>>(await server.Get($"/agents/{agent.Name}/run-groups"));

    private async Task ArchiveAsync(SeededAgent agent, bool archived, params string[] names) =>
        (
            await server.Post($"/agents/{agent.Name}/run-groups/archive", new ArchiveRunGroupsRequest(names, archived))
        ).EnsureSuccessStatusCode();

    private async Task<int> RunsOfAsync(params Guid[] runs)
    {
        await using var connection = await database.Pool.OpenAsync(Cancellation);
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM runs WHERE id = ANY(@runs)", new { runs });
    }

    [Fact]
    public async Task Archiving_a_group_marks_it_and_restoring_clears_it_while_other_groups_stay()
    {
        var agent = await SeedAsync();
        await FinishedRunAsync(agent, "sweep-1");
        await FinishedRunAsync(agent, "sweep-2");

        await ArchiveAsync(agent, true, "sweep-1");
        var archived = await GroupsAsync(agent);
        await ArchiveAsync(agent, false, "sweep-1");
        var restored = await GroupsAsync(agent);

        Assert.NotNull(archived.Single(group => group.Name == "sweep-1").ArchivedAt);
        Assert.Null(archived.Single(group => group.Name == "sweep-2").ArchivedAt);
        Assert.All(restored, group => Assert.Null(group.ArchivedAt));
    }

    [Fact]
    public async Task One_unknown_group_changes_nothing_and_is_404_like_an_unknown_agent()
    {
        var agent = await SeedAsync();
        await FinishedRunAsync(agent, "sweep-1");

        var unknown = await server.Post(
            $"/agents/{agent.Name}/run-groups/archive",
            new ArchiveRunGroupsRequest(["sweep-1", "no-such"], true)
        );
        var noAgent = await server.Post("/agents/no-such-agent/run-groups/archive", new ArchiveRunGroupsRequest(["sweep-1"], true));

        Assert.All([unknown, noAgent], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.All(await GroupsAsync(agent), group => Assert.Null(group.ArchivedAt));
    }

    [Fact]
    public async Task Deleting_an_archived_group_removes_its_runs_rechecks_samples_and_verdicts_and_nothing_else()
    {
        var agent = await SeedAsync();
        var first = await FinishedRunAsync(agent, "sweep-1", samples: 2);
        var recheck = await FinishedRunAsync(agent, null, recheckOf: first);
        var otherGroup = await FinishedRunAsync(agent, "sweep-2");
        var plain = await FinishedRunAsync(agent, null);
        await ArchiveAsync(agent, true, "sweep-1");

        var preview = await server.Get($"/agents/{agent.Name}/run-groups/sweep-1/deletion");
        var deleted = await server.Delete($"/agents/{agent.Name}/run-groups/sweep-1");

        Assert.Equal(new RunGroupDeletion("sweep-1", 2, 3, 3), await TestServer.Read<RunGroupDeletion>(preview));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, await RunsOfAsync(first, recheck));
        Assert.Equal(2, await RunsOfAsync(otherGroup, plain));
        Assert.Equal(["sweep-2"], (await GroupsAsync(agent)).Select(group => group.Name));
    }

    [Fact]
    public async Task A_group_that_is_not_archived_is_refused_with_409_and_kept()
    {
        var agent = await SeedAsync();
        var run = await FinishedRunAsync(agent, "sweep-1");

        var deleted = await server.Delete($"/agents/{agent.Name}/run-groups/sweep-1");
        var preview = await server.Get($"/agents/{agent.Name}/run-groups/sweep-1/deletion");

        Assert.All([deleted, preview], response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        Assert.Contains("archive it first", await deleted.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Equal(1, await RunsOfAsync(run));
    }

    [Fact]
    public async Task A_group_with_a_queued_run_is_refused_with_409_and_kept()
    {
        var agent = await SeedAsync();
        var queued = await new RunStore(database.Pool).EnqueueAsync(
            agent.Id,
            agent.VersionId,
            [],
            agent.EndpointUrl,
            runGroup: "sweep-1",
            cancellationToken: Cancellation
        );
        await ArchiveAsync(agent, true, "sweep-1");

        var deleted = await server.Delete($"/agents/{agent.Name}/run-groups/sweep-1");

        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Contains("queued or running", await deleted.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Equal(1, await RunsOfAsync(queued));
    }

    private async Task<string?> GroupNoteAsync(SeededAgent agent, string group) =>
        (await TestServer.Read<RunComparison>(await server.Get($"/runs/compare?group={group}&agent={agent.Name}"))).GroupNote;

    [Fact]
    public async Task A_group_note_is_trimmed_shown_by_the_comparison_of_the_group_only_changed_cleared_and_deleted_with_the_group()
    {
        var agent = await SeedAsync();
        var first = await FinishedRunAsync(agent, "sweep-1");
        await FinishedRunAsync(agent, "sweep-1");
        await FinishedRunAsync(agent, "sweep-2");

        var set = await server.Put($"/agents/{agent.Name}/run-groups/sweep-1/note", new NoteRequest { Note = "  three models \n" });

        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Equal("three models", await GroupNoteAsync(agent, "sweep-1"));
        Assert.Null(await GroupNoteAsync(agent, "sweep-2"));
        var byRuns = await TestServer.Read<RunComparison>(await server.Get($"/runs/compare?runs={first}"));
        Assert.Null(byRuns.GroupNote);

        await server.Put($"/agents/{agent.Name}/run-groups/sweep-1/note", new NoteRequest { Note = "other" });
        Assert.Equal("other", await GroupNoteAsync(agent, "sweep-1"));

        await server.Put($"/agents/{agent.Name}/run-groups/sweep-1/note", new NoteRequest { Note = "   " });
        Assert.Null(await GroupNoteAsync(agent, "sweep-1"));

        await server.Put($"/agents/{agent.Name}/run-groups/sweep-1/note", new NoteRequest { Note = "gone with the group" });
        await ArchiveAsync(agent, true, "sweep-1");
        Assert.Equal(HttpStatusCode.NoContent, (await server.Delete($"/agents/{agent.Name}/run-groups/sweep-1")).StatusCode);
        await using var connection = await database.Pool.OpenAsync(Cancellation);
        Assert.Equal(
            0,
            await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM run_group_notes WHERE agent_id = @id", new { id = agent.Id })
        );
    }

    [Fact]
    public async Task A_group_note_is_refused_for_an_unknown_agent_or_group_a_group_of_another_agent_a_bad_name_or_a_long_note()
    {
        var agent = await SeedAsync();
        var stranger = await SeedAsync();
        await FinishedRunAsync(agent, "sweep-1");
        await FinishedRunAsync(stranger, "theirs");
        var note = new NoteRequest { Note = "x" };

        var noAgent = await server.Put("/agents/no-such-agent/run-groups/sweep-1/note", note);
        var unknown = await server.Put($"/agents/{agent.Name}/run-groups/no-such/note", note);
        var foreign = await server.Put($"/agents/{agent.Name}/run-groups/theirs/note", note);
        var badName = await server.Put($"/agents/{agent.Name}/run-groups/Not_Kebab/note", note);
        var tooLong = await server.Put($"/agents/{agent.Name}/run-groups/sweep-1/note", new NoteRequest { Note = new string('x', 501) });

        Assert.All([noAgent, unknown, foreign], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.All([badName, tooLong], response => Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode));
        Assert.Null(await GroupNoteAsync(stranger, "theirs"));
    }

    [Fact]
    public async Task An_unknown_group_agent_or_a_group_of_another_agent_is_404()
    {
        var agent = await SeedAsync();
        var stranger = await SeedAsync();
        await FinishedRunAsync(stranger, "theirs");
        await ArchiveAsync(stranger, true, "theirs");

        var unknown = await server.Delete($"/agents/{agent.Name}/run-groups/no-such");
        var foreign = await server.Delete($"/agents/{agent.Name}/run-groups/theirs");
        var noAgent = await server.Delete("/agents/no-such-agent/run-groups/theirs");
        var noPreview = await server.Get($"/agents/{agent.Name}/run-groups/no-such/deletion");

        Assert.All([unknown, foreign, noAgent, noPreview], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.Single(await GroupsAsync(stranger));
    }

    [Fact]
    public async Task A_run_started_in_an_archived_group_puts_it_back_in_use()
    {
        var agent = await SeedAsync();
        await FinishedRunAsync(agent, "sweep-1");
        await ArchiveAsync(agent, true, "sweep-1");

        await FinishedRunAsync(agent, "sweep-1");

        var group = Assert.Single(await GroupsAsync(agent));
        Assert.Equal((2, null), (group.Runs, group.ArchivedAt));
    }

    [Fact]
    public async Task The_run_list_shows_runs_in_no_group_or_in_any_group_and_a_re_check_follows_its_run()
    {
        var agent = await SeedAsync();
        var plain = await FinishedRunAsync(agent, null);
        var grouped = await FinishedRunAsync(agent, "sweep-1");
        var recheckOfGrouped = await FinishedRunAsync(agent, null, recheckOf: grouped);

        async Task<List<Guid>> ListAsync(string query)
        {
            var list = await TestServer.Read<RunList>(await server.Get($"/runs?agent={agent.Name}{query}"));
            return [.. list.Runs.Select(run => run.Id)];
        }

        Assert.Equal([plain], await ListAsync("&grouping=None"));
        Assert.Equal([recheckOfGrouped, grouped], await ListAsync("&grouping=Any"));
        Assert.Equal(3, (await ListAsync("")).Count);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (await server.Get($"/runs?agent={agent.Name}&group=sweep-1&grouping=None")).StatusCode
        );
    }
}
