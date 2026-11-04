extern alias Cli;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Runs;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Evaluation.Runs;
using Suwageval.Server.Tests.Evaluation.Runs;
using Suwageval.Server.Tests.Evaluation.Scenarios;
using CliApp = Cli::Suwageval.Cli.CliApp;

namespace Suwageval.Server.Tests.EndToEnd;

public sealed class GateCommandEndToEndTests(TestDatabase database) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();
    private readonly string work = Directory.CreateTempSubdirectory("suwageval-gate-").FullName;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static ScenarioInput Guarded(string key, string id, int? retryLimit = null) =>
        SeededAgent.Scenario(key, conversation: [new UserMessage { Text = $"Please refund order A-17 ({key})." }]) with
        {
            AcGuardrail = new AcGuardrail { Id = id, RetryLimit = retryLimit },
        };

    private Task<SeededAgent> SeedAsync(string endpoint, params ScenarioInput[] scenarios) =>
        SeededAgent.CreateAsync(
            database.Pool,
            endpoint,
            scenarios,
            [.. scenarios.Select(scenario => SeededAgent.RefundAfterLookup($"{scenario.Key}-refunded", scenario.Key))]
        );

    private async Task<(int Exit, string Output, string Error)> GateAsync(SeededAgent agent, params string[] args)
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
                    var exit = CliApp.Run(["gate", "--dir", work, .. args], new StringReader(""), output, error, http);
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

    [Fact]
    [Trait("criterion", "AC-gate-runs-8")]
    [Trait("criterion", "AC-gate-runs-9")]
    public async Task Every_criterion_passing_exits_0_with_one_line()
    {
        await using var fake = await FakeAgent.StartAsync(
            (_, call) => Results.Text(call == 1 ? FakeAgent.PlainTurn : FakeAgent.RefundTurn, "application/json")
        );
        var agent = await SeedAsync(fake.Url.AbsoluteUri, Guarded("refund", ExampleCriteria.Refunds1));

        var (exit, output, error) = await GateAsync(agent);

        Assert.Equal(0, exit);
        Assert.Equal("PASSED 1 of 1 acceptance criteria", output.Trim());
        Assert.Empty(error);
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-7")]
    [Trait("criterion", "AC-gate-runs-9")]
    public async Task A_failed_criterion_exits_1_with_every_attempts_failed_checks_and_what_the_agent_did_last()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.PlainTurn);
        var agent = await SeedAsync(fake.Url.AbsoluteUri, Guarded("a-refund", ExampleCriteria.Refunds1, retryLimit: 2));

        var (exit, output, _) = await GateAsync(agent);

        Assert.Equal(1, exit);
        var lines = output.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal($"FAILED {ExampleCriteria.Refunds1} (scenario a-refund): 2 of 2 attempts failed", lines[0]);
        Assert.StartsWith("  attempt 1: a-refund-refunded: ", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("  attempt 2: a-refund-refunded: ", lines[2], StringComparison.Ordinal);
        Assert.Equal("  what the agent did in the last attempt:", lines[3]);
        Assert.Contains(lines, line => line.StartsWith("    assistant: ", StringComparison.Ordinal));
        Assert.StartsWith(
            $"FAILED {ExampleCriteria.Refunds1} (0 of 1 acceptance criteria passed); everything: suwageval results ",
            lines[^1],
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            lines,
            line => line.Contains("passed ", StringComparison.Ordinal) && !line.StartsWith("FAILED", StringComparison.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-9")]
    public async Task With_json_the_results_carry_the_verdict()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeedAsync(fake.Url.AbsoluteUri, Guarded("refund", ExampleCriteria.Refunds1));

        var (exit, output, _) = await GateAsync(agent, "--json");

        Assert.Equal(0, exit);
        var results = JsonSerializer.Deserialize<RunResults>(output, ContractJson.Options);
        Assert.Equal(new GateResult { Passed = true }, results?.Gate);
        Assert.Equal(1, results?.Scenarios.Single().AcGuardrail?.Attempts);
    }

    [Fact]
    [Trait("criterion", "AC-gate-runs-8")]
    public async Task An_agent_without_guarded_scenarios_exits_2()
    {
        await using var fake = await FakeAgent.AlwaysAsync(FakeAgent.RefundTurn);
        var agent = await SeedAsync(fake.Url.AbsoluteUri, SeededAgent.Scenario("refund"));

        var (exit, _, error) = await GateAsync(agent);

        Assert.Equal(2, exit);
        Assert.Contains("no scenario with an acGuardrail", error, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        Directory.Delete(work, recursive: true);
    }
}
