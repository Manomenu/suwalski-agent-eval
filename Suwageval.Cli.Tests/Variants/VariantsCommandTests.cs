using System.Net;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Variants;

/// <summary>The command against a stand-in for the API: what it asks for and what it prints.</summary>
public sealed class VariantsCommandTests
{
    private static readonly Guid First = Guid.Parse("11111111-2222-3333-4444-555555555551");
    private static readonly Guid Second = Guid.Parse("11111111-2222-3333-4444-555555555552");

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval");

    private static VariantRun Column(Guid id, string? label, int checksPassed, int checksFailed) =>
        new()
        {
            Run = new RunSummary
            {
                Id = id,
                Agent = "example-agent",
                AgentVersionId = Guid.NewGuid(),
                EndpointUrl = "http://localhost:7010",
                Status = RunStatus.Done,
                SamplesTotal = 4,
                SamplesDone = 4,
                SamplesFailed = 1,
                ChecksPassed = checksPassed,
                ChecksFailed = checksFailed,
                CreatedAt = DateTime.UtcNow,
            },
            VersionLabel = label,
            ModelNames = ["gpt-4o"],
            MedianMs = 1500,
            P95Ms = 3000,
        };

    private static readonly RunVariants Variants = new()
    {
        Agent = "example-agent",
        Group = "sweep-1",
        Runs = [Column(First, "gpt-4o", 75, 22), Column(Second, null, 80, 17)],
        Tags =
        [
            new ComparedTag { Tag = null, Rates = [new ComparedRate(0.77), new ComparedRate(0.82)] },
            new ComparedTag { Tag = "billing", Rates = [new ComparedRate(0.5), new ComparedRate()] },
        ],
        Scenarios =
        [
            new VariantScenario
            {
                Key = "refund",
                Tags = ["billing"],
                Cells =
                [
                    new VariantCell
                    {
                        ChecksPassed = 3,
                        ChecksTotal = 4,
                        FailedChecks = ["refund-event"],
                        Samples = 1,
                        SamplesFailed = 0,
                    },
                    null,
                ],
            },
        ],
    };

    private sealed class FakeApi : HttpMessageHandler
    {
        public List<string> PathsAndQueries { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            PathsAndQueries.Add(request.RequestUri?.PathAndQuery ?? "");
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(Variants, ContractJson.Options),
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
        }
    }

    private static (int Exit, string Output, string Error, FakeApi Api) Variant(params string[] args)
    {
        var api = new FakeApi();
        using var http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliApp.Run(["variants", .. args, "--dir", Sample], new StringReader(""), output, error, http);
        return (exit, output.ToString(), error.ToString(), api);
    }

    [Fact]
    public void Runs_are_printed_as_a_lettered_column_each_with_checks_per_scenario_and_the_failed_ones()
    {
        var (exit, output, _, api) = Variant(First.ToString(), Second.ToString());

        Assert.Equal(0, exit);
        Assert.Equal([$"/runs/variants?runs={First}&runs={Second}"], api.PathsAndQueries);
        Assert.Contains(
            "A  gpt-4o  models gpt-4o  run 11111111  checks 75/97 passed, 22 failed  samples 3/4 answered",
            output,
            StringComparison.Ordinal
        );
        Assert.Contains("billing  50%", output, StringComparison.Ordinal);
        Assert.Contains("refund    3/4", output, StringComparison.Ordinal);
        Assert.Contains("A failed: refund-event", output, StringComparison.Ordinal);
        Assert.Contains("page: http://localhost:3230/variants?runs=", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_group_is_asked_for_by_name_and_the_agent_of_agent_yaml()
    {
        var (exit, output, _, api) = Variant("--group", "sweep-1");

        Assert.Equal(0, exit);
        Assert.Equal(["/runs/variants?group=sweep-1&agent=example-agent"], api.PathsAndQueries);
        Assert.Contains("variants of example-agent (group sweep-1)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ids_and_a_group_together_or_neither_are_refused_before_any_request()
    {
        var (bothExit, _, _, bothApi) = Variant(First.ToString(), "--group", "sweep-1");
        var (neitherExit, _, _, neitherApi) = Variant();

        Assert.NotEqual(0, bothExit);
        Assert.NotEqual(0, neitherExit);
        Assert.Empty(bothApi.PathsAndQueries);
        Assert.Empty(neitherApi.PathsAndQueries);
    }
}
