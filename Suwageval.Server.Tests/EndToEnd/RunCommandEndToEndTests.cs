extern alias Cli;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;
using Suwageval.Server.Tests.Runs;
using CliApp = Cli::Suwageval.Cli.CliApp;

namespace Suwageval.Server.Tests.EndToEnd;

/// <summary>
/// <c>suwageval run --wait</c> from the CLI through the server in process to a fake agent over HTTP and back — the MVP's path
/// without a model. The test plays the worker: it takes the agent's queued runs, as the server's own worker would.
/// </summary>
public sealed class RunCommandEndToEndTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();
    private readonly string work = Directory.CreateTempSubdirectory("suwageval-run-").FullName;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Task<(int Exit, string Output, string Error)> RunCliAsync(SeededAgent agent, params string[] args) =>
        CliAsync(agent, ["run", "--dir", work, .. args]);

    private async Task<(int Exit, string Output, string Error)> CliAsync(SeededAgent agent, string[] command)
    {
        await File.WriteAllTextAsync(
            Path.Combine(work, "agent.yaml"),
            $"name: {agent.Name}\nendpointUrl: {agent.EndpointUrl}\n",
            Cancellation
        );
        using var http = app.CreateClient();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var worker = Task.Run(
            async () =>
            {
                var runs = app.Services.GetRequiredService<RunStore>();
                var executor = app.Services.GetRequiredService<RunExecutor>();
                while (!stop.IsCancellationRequested)
                {
                    var run = await runs.ClaimNextAsync(agent.Id, stop.Token);
                    if (run is null)
                    {
                        await Task.Delay(50, stop.Token);
                        continue;
                    }
                    await executor.ExecuteAsync(run, stop.Token);
                }
            },
            Cancellation
        );
        try
        {
            return await Task.Run(
                () =>
                {
                    using var output = new StringWriter();
                    using var error = new StringWriter();
                    var exit = CliApp.Run(command, new StringReader(""), output, error, http);
                    return (exit, output.ToString(), error.ToString());
                },
                Cancellation
            );
        }
        finally
        {
            await stop.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
        }
    }

    private Task<SeededAgent> SeedAsync(string endpoint) =>
        SeededAgent.CreateAsync(
            database.Pool,
            endpoint,
            [SeededAgent.Scenario("refund", ["regression"])],
            [SeededAgent.RefundAfterLookup("refund-called", "refund"), SeededAgent.RefundedEvent("refund-event", "refund")]
        );

    [Fact]
    public async Task All_checks_passing_exits_0_and_prints_the_totals_and_the_runs_page()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeedAsync(fake.Url.AbsoluteUri);

        var (exit, output, error) = await RunCliAsync(agent, "--tag", "regression", "--wait");

        Assert.Equal(0, exit);
        Assert.Contains("2 checks: 2 passed, 0 failed", output, StringComparison.Ordinal);
        Assert.Contains("/runs/", output, StringComparison.Ordinal);
        Assert.Empty(error);
    }

    [Fact]
    public async Task A_failing_check_exits_1_and_its_json_says_why()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeedAsync(fake.Url.AbsoluteUri);

        var (exit, output, _) = await RunCliAsync(agent, "--tag", "regression", "--wait", "--json");

        Assert.Equal(1, exit);
        var results = JsonSerializer.Deserialize<RunResults>(output, ContractJson.Options);
        var check = Assert.Single(results?.Scenarios.Single().Checks ?? [], check => check.Key == "refund-called");
        Assert.False(check.Passed);
        Assert.Contains("refund_order was not called", check.Samples.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_endpoint_given_on_the_command_line_replaces_the_registered_one()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeedAsync("http://127.0.0.1:1");

        var (exit, _, _) = await RunCliAsync(agent, "--tag", "regression", "--wait", "--endpoint", fake.Url.AbsoluteUri);

        Assert.Equal(0, exit);
        Assert.Single(fake.Requests);
    }

    [Fact]
    public async Task Without_wait_the_run_id_is_printed_and_the_exit_is_0()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeedAsync(fake.Url.AbsoluteUri);

        var (exit, output, _) = await RunCliAsync(agent, "--tag", "regression");

        Assert.Equal(0, exit);
        Assert.True(Guid.TryParse(output.Trim().Split('\n')[^1], out _));
    }

    [Fact]
    public async Task The_version_comes_from_the_running_agent_new_once_and_existing_again_until_it_changes()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        fake.ServeContract(SeededAgent.ExampleVersionConfigured("Fresh for this test."));
        var agent = await SeedAsync(fake.Url.AbsoluteUri);

        var (_, first, _) = await RunCliAsync(agent, "--tag", "regression", "--wait");
        var (_, second, _) = await RunCliAsync(agent, "--tag", "regression", "--wait");
        fake.ServeContract(SeededAgent.ExampleVersionConfigured("Redeployed."));
        var (_, third, _) = await RunCliAsync(agent, "--tag", "regression", "--wait");

        Assert.Contains(": new", first.Split('\n')[0], StringComparison.Ordinal);
        Assert.Contains(": existing", second.Split('\n')[0], StringComparison.Ordinal);
        Assert.Contains(": new", third.Split('\n')[0], StringComparison.Ordinal);
        Assert.NotEqual(first.Split('\n')[0], third.Split('\n')[0]);
    }

    [Fact]
    public async Task An_agent_that_cannot_say_what_it_is_exits_2_naming_the_contract_url()
    {
        var agent = await SeedAsync("http://127.0.0.1:1");

        var (exit, _, error) = await RunCliAsync(agent, "--tag", "regression", "--wait");

        Assert.Equal(2, exit);
        Assert.Contains("502", error, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:1/suwageval/contract", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_version_that_does_not_fit_exits_2_listing_the_mismatches()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        fake.ServeContract(
            new AgentContract
            {
                Agents =
                [
                    new LlmAgentDefinition
                    {
                        Name = "a",
                        Model = new ModelDefinition { Name = "m" },
                        Config = "p",
                    },
                ],
            }
        );
        var agent = await SeededAgent.CreateAsync(
            database.Pool,
            fake.Url.AbsoluteUri,
            [SeededAgent.Scenario("refund")],
            [SeededAgent.RefundAfterLookup("refund-called", "refund")]
        );

        var (exit, _, error) = await RunCliAsync(agent, "--wait");

        Assert.Equal(2, exit);
        Assert.Contains("409", error, StringComparison.Ordinal);
        Assert.Contains("refund_order", error, StringComparison.Ordinal);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task Rerun_repeats_a_run_of_the_agent_with_its_tag_and_prints_like_run()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeedAsync(fake.Url.AbsoluteUri);
        var (_, first, _) = await RunCliAsync(agent, "--tag", "regression");
        var original = first.Trim().Split('\n')[^1];

        var (exit, output, error) = await CliAsync(agent, ["rerun", original, "--wait"]);

        Assert.Equal(0, exit);
        Assert.Contains(": existing", output, StringComparison.Ordinal);
        Assert.Contains("2 checks: 2 passed, 0 failed", output, StringComparison.Ordinal);
        Assert.Empty(error);
    }

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        Directory.Delete(work, recursive: true);
    }
}
