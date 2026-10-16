using Suwageval.Cli.Files;
using Suwageval.Cli.Validation;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Tests.Validation;

public sealed class ValidatorTests
{
    /// <summary>The scenario against the agent file and the contract the running agent serves (none: names are not checked).</summary>
    private static ValidationResult Validate(string scenario, AgentContract? contract = null, bool offline = false) =>
        Validator.Validate(
            "agent.yaml",
            TestAgent.Yaml,
            [new SourceText("s.yaml", scenario)],
            offline ? null : contract ?? TestAgent.Contract
        );

    private static ValidationResult ValidateAgentFile(string agent) =>
        Validator.Validate("agent.yaml", agent, [new SourceText("s.yaml", TestAgent.Scenario)]);

    private static void AssertProblem(ValidationResult result, string path, string messagePart)
    {
        Assert.True(
            result.Problems.Any(p => p.Path == path && p.Message.Contains(messagePart, StringComparison.Ordinal)),
            $"Expected '{path}: …{messagePart}…' among:\n{string.Join('\n', TestAgent.Lines(result.Problems))}"
        );
    }

    [Fact]
    public void A_correct_scenario_is_valid_and_counted()
    {
        var result = Validate(TestAgent.Scenario);

        Assert.Empty(result.Problems);
        Assert.Equal((1, 1), (result.Scenarios, result.Checks));
    }

    [Fact]
    public void A_scenario_without_checks_is_valid() =>
        Assert.True(Validate("key: only-a-conversation\nconversation:\n  - user: hi\n").IsValid);

    [Fact]
    public void A_missing_agent_file_is_a_problem()
    {
        var result = Validator.Validate("agent.yaml", null, [new SourceText("s.yaml", TestAgent.Scenario)]);

        Assert.Equal("agent.yaml", Assert.Single(result.Problems).File);
    }

    [Fact]
    public void A_scenario_is_still_checked_without_an_agent_file()
    {
        var result = Validator.Validate("agent.yaml", null, [new SourceText("s.yaml", "key: Bad Key\nconversation:\n  - user: hi\n")]);

        AssertProblem(result, "key", "kebab-case");
    }

    [Theory]
    [InlineData("name: x\n", "Missing required field: endpointUrl")]
    [InlineData("endpointUrl: http://x\n", "Missing required field: name")]
    [InlineData("name: x\nendpointUrl: http://x\nbogus: 1\n", "Unknown field 'bogus'")]
    public void The_agent_file_is_checked_against_the_contract(string agent, string messagePart)
    {
        var result = ValidateAgentFile(agent);

        Assert.Contains(result.Problems, p => p.File == "agent.yaml" && p.Message.Contains(messagePart, StringComparison.Ordinal));
    }

    [Fact]
    public void A_version_in_the_agent_file_is_refused_because_versions_come_from_the_agent()
    {
        var result = ValidateAgentFile("name: x\nendpointUrl: http://x\nversion:\n  systemPrompt: p\n  model: { name: m }\n");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(("agent.yaml", "version"), (problem.File, problem.Path));
        Assert.Contains("versions come from the agent's /suwageval/contract", problem.Message, StringComparison.Ordinal);
        Assert.Equal(3L, problem.Line);
    }

    [Fact]
    public void Without_the_agents_contract_names_are_not_checked_but_the_structure_still_is()
    {
        var scenario =
            "key: k\nconversation:\n  - event: { type: nope, data: {} }\nchecks:\n  - { key: c, type: tool-called, tool: nope }\n";

        Assert.True(Validate(scenario, offline: true).IsValid);
        Assert.Equal(2, Validate(scenario).Problems.Count);
        AssertProblem(Validate("key: Bad\nconversation:\n  - user: hi\n", offline: true), "key", "kebab-case");
    }

    [Fact]
    public void An_unknown_field_is_a_problem_at_its_place()
    {
        var result = Validate(TestAgent.Scenario + "\nsamplez: 2\n");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(("samplez", "Unknown field 'samplez'."), (problem.Path, problem.Message));
        Assert.Equal(9L, problem.Line);
    }

    [Theory]
    [InlineData("key: x\n", "Missing required field: conversation")]
    [InlineData("conversation:\n  - user: hi\n", "Missing required field: key")]
    [InlineData("key: x\nconversation: []\n", "no items")]
    [InlineData("- just\n- a list\n", "mapping")]
    public void A_scenario_needs_its_required_fields(string scenario, string messagePart)
    {
        var result = Validate(scenario);

        Assert.Contains(result.Problems, p => p.Message.Contains(messagePart, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Refund")]
    [InlineData("refund_flow")]
    [InlineData("refund flow")]
    [InlineData("-refund")]
    [InlineData("refund--flow")]
    public void A_scenario_key_must_be_kebab_case(string key) =>
        AssertProblem(Validate($"key: {key}\nconversation:\n  - user: hi\n"), "key", "kebab-case");

    [Fact]
    public void A_tag_must_be_kebab_case() =>
        AssertProblem(Validate("key: k\ntags: [ok, Not Ok]\nconversation:\n  - user: hi\n"), "tags[1]", "kebab-case");

    [Fact]
    public void Scenario_keys_are_unique_across_files()
    {
        var result = Validator.Validate(
            "agent.yaml",
            TestAgent.Yaml,
            [
                new SourceText("one.yaml", TestAgent.Scenario),
                new SourceText("two.yaml", TestAgent.Scenario.Replace("refund-called", "other", StringComparison.Ordinal)),
            ]
        );

        var problem = Assert.Single(result.Problems);
        Assert.Equal(("two.yaml", "key"), (problem.File, problem.Path));
        Assert.Contains("one.yaml", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_keys_are_unique_across_files()
    {
        var result = Validator.Validate(
            "agent.yaml",
            TestAgent.Yaml,
            [
                new SourceText("one.yaml", TestAgent.Scenario),
                new SourceText("two.yaml", TestAgent.Scenario.Replace("refund-happy-path", "second", StringComparison.Ordinal)),
            ]
        );

        var problem = Assert.Single(result.Problems);
        Assert.Equal(("two.yaml", "checks[0].key"), (problem.File, problem.Path));
    }

    [Fact]
    public void A_check_key_must_be_kebab_case() =>
        AssertProblem(
            Validate(TestAgent.Scenario.Replace("refund-called", "Refund Called", StringComparison.Ordinal)),
            "checks[0].key",
            "kebab-case"
        );

    [Theory]
    [InlineData("samples: 0", "samples")]
    [InlineData("continuation: { turns: 0 }", "continuation.turns")]
    public void Counts_are_at_least_one(string field, string path) =>
        AssertProblem(Validate($"key: k\n{field}\nconversation:\n  - user: hi\n"), path, "at least 1");

    [Fact]
    public void A_tool_result_must_answer_an_earlier_tool_call()
    {
        var scenario = """
            key: k
            conversation:
              - tool_result: { call_id: c1, content: ok }
              - tool_call: { id: c1, name: get_order, arguments: {} }
              - tool_result: { call_id: c1, content: ok }
            """;

        var result = Validate(scenario);

        var problem = Assert.Single(result.Problems);
        Assert.Equal("conversation[0].tool_result.call_id", problem.Path);
        Assert.Equal(3L, problem.Line);
    }

    [Fact]
    public void A_retrieval_needs_the_agent_to_use_retrieval()
    {
        var scenario = "key: k\nconversation:\n  - kind: Retrieval\n    query: q\n    chunks: [{ text: t, source: s, score: 0.5 }]\n";

        AssertProblem(Validate(scenario), "conversation[0]", "usesRetrieval");
        Assert.True(Validate(scenario, TestAgent.WithRetrieval).IsValid);
    }

    [Fact]
    public void An_event_in_the_conversation_must_be_declared()
    {
        var scenario = "key: k\nconversation:\n  - event: { type: other.event, data: {} }\n  - event: { type: state.changed, data: {} }\n";

        var problem = Assert.Single(Validate(scenario).Problems);
        Assert.Equal("conversation[0].event.type", problem.Path);
    }

    [Theory]
    [InlineData("type: tool-called\n    tool: nope", "checks[0].tool")]
    [InlineData("type: tool-not-called\n    tool: nope", "checks[0].tool")]
    [InlineData("type: event\n    event: nope", "checks[0].event")]
    public void A_check_names_only_what_the_agent_has(string check, string path)
    {
        var result = Validate($"key: k\nconversation:\n  - user: hi\nchecks:\n  - key: c\n    {check}\n");

        AssertProblem(result, path, "nope");
    }

    [Theory]
    [InlineData("after", "refund_order")]
    [InlineData("before", "state.changed")]
    public void Where_may_name_a_tool_or_an_event_type(string anchor, string name)
    {
        var scenario =
            $"key: k\nconversation:\n  - user: hi\nchecks:\n  - key: c\n    type: tool-not-called\n    tool: get_order\n    where: {{ {anchor}: {name} }}\n";

        Assert.True(Validate(scenario).IsValid);
        AssertProblem(Validate(scenario.Replace(name, "nope", StringComparison.Ordinal)), $"checks[0].where.{anchor}", "nope");
    }

    [Fact]
    public void A_text_check_needs_something_to_check() =>
        AssertProblem(Validate("key: k\nconversation:\n  - user: hi\nchecks:\n  - { key: c, type: text }\n"), "checks[0]", "at least one");

    [Fact]
    public void A_text_pattern_must_be_a_regular_expression()
    {
        var result = Validate("key: k\nconversation:\n  - user: hi\nchecks:\n  - { key: c, type: text, pattern: '([' }\n");

        AssertProblem(result, "checks[0].pattern", "regular expression");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1.5")]
    [InlineData("-1")]
    public void A_pass_threshold_is_above_zero_and_at_most_one(string threshold)
    {
        var result = Validate(
            $"key: k\nconversation:\n  - user: hi\nchecks:\n  - {{ key: c, type: text, contains: x, passThreshold: {threshold} }}\n"
        );

        AssertProblem(result, "checks[0].passThreshold", "above 0");
    }

    [Theory]
    [InlineData("type: judge", "Unknown check type 'judge'")]
    [InlineData("tool: refund_order", "needs a 'type'")]
    [InlineData("type: tool-called\n    tool: refund_order\n    where: { turn: soon }", "turn must be")]
    [InlineData("type: tool-called\n    tool: refund_order\n    where: { turn: 0 }", "turn must be")]
    [InlineData("type: tool-called\n    tool: refund_order\n    scenarioKey: other", "Unknown field 'scenarioKey'")]
    [InlineData("type: tool-called", "Missing required field: tool")]
    public void A_check_must_match_the_contract(string check, string messagePart)
    {
        var result = Validate($"key: k\nconversation:\n  - user: hi\nchecks:\n  - key: c\n    {check}\n");

        Assert.Contains(
            result.Problems,
            p => p.Path.StartsWith("checks[0]", StringComparison.Ordinal) && p.Message.Contains(messagePart, StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData("turn: 2")]
    [InlineData("turn: last")]
    [InlineData("turn: any")]
    public void The_turn_of_where_is_a_number_last_or_any(string where)
    {
        var scenario =
            $"key: k\nconversation:\n  - user: hi\nchecks:\n  - key: c\n    type: tool-called\n    tool: refund_order\n    where: {{ {where} }}\n";

        Assert.True(Validate(scenario).IsValid);
    }

    [Theory]
    [InlineData("- kind: Nope\n    text: x", "Unknown kind 'Nope'")]
    [InlineData("- kind: UserMessage", "Missing required field: text")]
    [InlineData("- kind: UserMessage\n    text: x\n    more: 1", "Unknown field 'more'")]
    [InlineData("- tool_call: { id: c1, name: n }", "arguments")]
    [InlineData("- user: 5", "text")]
    public void A_conversation_item_must_match_the_contract(string item, string messagePart)
    {
        var result = Validate($"key: k\nconversation:\n  {item}\n");

        Assert.Contains(
            result.Problems,
            p => p.Path.StartsWith("conversation[0]", StringComparison.Ordinal) && p.Message.Contains(messagePart, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Every_problem_of_a_file_is_reported_not_only_the_first()
    {
        var scenario =
            "key: k\nconversation:\n  - shout: x\n  - kind: Nope\n  - user: ok\nchecks:\n  - { key: a, type: judge }\n  - { key: b, type: tool-called }\n";

        var paths = Validate(scenario).Problems.Select(p => p.Path);

        Assert.Equal(["conversation[0]", "conversation[1]", "checks[0]", "checks[1]"], paths);
    }
}
