extern alias Cli;

using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Server.Evaluation.Scenarios;
using ScenarioFileReader = Cli::Suwageval.Cli.Files.ScenarioFileReader;

namespace Suwageval.Server.Tests.EndToEnd;

/// <summary>The YAML the run page shows is a scenario file: the CLI reads it back into the same scenario, without its samples and checks.</summary>
public sealed class ScenarioYamlRoundTripTests
{
    [Fact]
    public void A_rendered_scenario_is_read_by_the_cli_into_the_same_scenario_and_checks()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Sample", "suwageval", "scenarios", "refund-follow-up.yaml");
        var problems = new List<Cli::Suwageval.Cli.Files.Problem>();
        var original = ScenarioFileReader.Read(path, File.ReadAllText(path), problems);
        Assert.NotNull(original);

        var yaml = ScenarioYaml.Of(original.Scenario);
        var reread = ScenarioFileReader.Read(path, yaml, problems);

        Assert.Empty(problems);
        Assert.NotNull(reread);
        Assert.Equal(Json(original.Scenario with { Samples = null }), Json(reread.Scenario));
        Assert.DoesNotContain("checks:", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("samples:", yaml, StringComparison.Ordinal);
        Assert.Contains("\n- user: ", yaml, StringComparison.Ordinal);
        Assert.Contains("\n- tool_result:", yaml, StringComparison.Ordinal);
        Assert.Contains("\n- kind: AssistantMessage", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scenario_drafted_from_a_conversation_is_a_valid_scenario_file_that_remembers_its_conversation()
    {
        var id = Guid.NewGuid();
        var draft = Suwageval.Server.ScenarioDrafts.ScenarioDraftWriter.Draft(
            "chat-81",
            id,
            Suwageval.Server.Tests.ScenarioDrafts.ScenarioDraftWriterTests.Turns()
        );
        var problems = new List<Cli::Suwageval.Cli.Files.Problem>();

        var read = ScenarioFileReader.Read(draft.Path, draft.Yaml, problems);

        Assert.Empty(problems);
        Assert.NotNull(read);
        Assert.Equal("conversation-chat-81", read.Scenario.Key);
        Assert.Equal(id, read.Scenario.Origin?.MonitoringConversation);
        Assert.IsType<Suwageval.AgentSdk.Conversations.UserMessage>(read.Scenario.Conversation[^1]);
        Assert.Empty(read.Checks);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ContractJson.Options);
}
