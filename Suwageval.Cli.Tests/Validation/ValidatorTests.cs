using Suwageval.AgentSdk.Agents;
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
    public void A_regression_block_is_read_into_the_agent()
    {
        var result = ValidateAgentFile("name: x\nendpointUrl: http://x\nregression:\n  branches: [master, release]\n  history: 3\n");

        Assert.True(result.IsValid);
        var settings = result.Agent?.Definition.Regression;
        Assert.Equal(["master", "release"], settings?.Branches);
        Assert.Equal(3, settings?.History);
    }

    [Theory]
    [InlineData("regression:\n  branches: []\n", "regression.branches", 4L)]
    [InlineData("regression:\n  branches: [master, master]\n", "regression.branches[1]", 4L)]
    [InlineData("regression:\n  branches: ['a b']\n", "regression.branches[0]", 4L)]
    [InlineData("regression:\n  history: 0\n", "regression.history", 4L)]
    [InlineData("regression:\n  history: 21\n", "regression.history", 4L)]
    public void A_bad_regression_block_is_a_problem_at_its_place_in_the_file(string block, string path, long line)
    {
        var result = ValidateAgentFile("name: x\nendpointUrl: http://x\n" + block);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(("agent.yaml", path, line), (problem.File, problem.Path, problem.Line));
    }

    [Fact]
    public void An_agents_samples_are_read_into_the_agent()
    {
        var result = ValidateAgentFile("name: x\nendpointUrl: http://x\nsamples: 5\n");

        Assert.True(result.IsValid);
        Assert.Equal(5, result.Agent?.Definition.Samples);
        Assert.Null(ValidateAgentFile("name: x\nendpointUrl: http://x\n").Agent?.Definition.Samples);
    }

    [Theory]
    [InlineData("samples: 0")]
    [InlineData("samples: 21")]
    public void An_agents_samples_out_of_range_are_a_problem_at_their_line(string field)
    {
        var result = ValidateAgentFile("name: x\nendpointUrl: http://x\n" + field + "\n");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(("agent.yaml", "samples", 3L), (problem.File, problem.Path, problem.Line));
        Assert.Contains("from 1 to 20", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_suwageval_url_is_the_clis_own_and_not_part_of_the_pushed_agent()
    {
        var result = ValidateAgentFile("name: x\nendpointUrl: http://x\nsuwagevalUrl: http://suwageval.example\n");

        Assert.True(result.IsValid);
        Assert.Equal("http://suwageval.example", result.Agent?.SuwagevalUrl);
        Assert.Equal(new AgentDefinition { Name = "x", EndpointUrl = "http://x" }, result.Agent?.Definition);
    }

    [Theory]
    [InlineData("suwagevalUrl: localhost:3230")]
    [InlineData("suwagevalUrl: ftp://x")]
    [InlineData("suwagevalUrl: 5")]
    public void A_suwageval_url_that_is_no_http_address_is_a_problem_at_its_line(string field)
    {
        var result = ValidateAgentFile("name: x\nendpointUrl: http://x\n" + field + "\n");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(("agent.yaml", "suwagevalUrl", 3L), (problem.File, problem.Path, problem.Line));
        Assert.Contains("absolute http or https address", problem.Message, StringComparison.Ordinal);
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

    [Fact]
    public void Turns_are_at_least_one() =>
        AssertProblem(Validate("key: k\ncontinuation: { turns: 0 }\nconversation:\n  - user: hi\n"), "continuation.turns", "at least 1");

    [Theory]
    [InlineData("samples: 0")]
    [InlineData("samples: 21")]
    public void A_scenarios_samples_are_from_one_to_twenty(string field) =>
        AssertProblem(Validate($"key: k\n{field}\nconversation:\n  - user: hi\n"), "samples", "from 1 to 20 (the default is 3)");

    [Theory]
    [InlineData("0")]
    [InlineData("21")]
    public void A_checks_samples_are_from_one_to_twenty(string samples) =>
        AssertProblem(
            Validate($"key: k\nconversation:\n  - user: hi\nchecks:\n  - {{ key: c, type: text, contains: x, samples: {samples} }}\n"),
            "checks[0].samples",
            "checks[0].samples is from 1 to 20"
        );

    [Fact]
    public void Samples_set_on_a_scenario_and_a_check_are_read()
    {
        var result = Validate(
            "key: k\nsamples: 5\nconversation:\n  - user: hi\nchecks:\n  - { key: c, type: text, contains: x, samples: 1 }\n"
        );

        Assert.True(result.IsValid);
    }

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

    [Fact]
    public void A_tool_call_or_check_by_an_agent_needs_that_agent_to_exist_and_have_the_tool()
    {
        var scenario = """
            key: k
            conversation:
              - tool_call: { id: c1, name: charge_card, arguments: {}, agent: billing-agent }
              - tool_call: { id: c2, name: charge_card, arguments: {}, agent: support-agent }
              - tool_call: { id: c3, name: get_order, arguments: {}, agent: ghost }
              - tool_call: { id: c4, name: charge_card, arguments: {} }
            checks:
              - key: a
                type: tool-called
                tool: charge_card
                by: billing-agent
              - key: b
                type: tool-called
                tool: charge_card
                by: support-agent
              - key: c
                type: tool-not-called
                tool: get_order
                by: ghost
            """;

        var result = Validate(scenario);

        AssertProblem(result, "conversation[1].tool_call.agent", "The agent 'support-agent' has no tool 'charge_card'");
        AssertProblem(result, "conversation[2].tool_call.agent", "The system has no agent 'ghost'");
        AssertProblem(result, "checks[1].by", "The agent 'support-agent' has no tool 'charge_card'");
        AssertProblem(result, "checks[2].by", "The system has no agent 'ghost'");
        Assert.Equal(4, result.Problems.Count);
    }

    [Fact]
    public void A_tool_no_agent_has_is_a_problem_in_the_conversation_too()
    {
        var scenario = """
            key: k
            conversation:
              - tool_call: { id: c1, name: nope, arguments: {} }
            """;

        AssertProblem(Validate(scenario), "conversation[0].tool_call.name", "No agent has the tool 'nope'");
    }

    [Fact]
    public void Sources_and_an_optional_score_are_read()
    {
        var scenario = """
            key: k
            conversation:
              - kind: AssistantMessage
                text: hi
                sources: [plans, faq]
              - kind: Retrieval
                query: q
                chunks: [{ text: t, source: s }]
            """;

        Assert.True(Validate(scenario, TestAgent.WithRetrieval).IsValid);
    }

    [Fact]
    public void Fixtures_are_read_and_need_an_agent_that_declares_a_fixtures_schema()
    {
        var scenario = "key: k\nfixtures:\n  pinnedResources: [plans]\nconversation:\n  - user: hi\n";

        Assert.True(Validate(scenario, TestAgent.WithFixtures).IsValid);
        AssertProblem(Validate(scenario), "fixtures", "fixturesSchema");
        Assert.True(Validate(scenario, offline: true).IsValid);
    }

    [Fact]
    public void A_scenario_without_fixtures_needs_no_schema() => Assert.True(Validate("key: k\nconversation:\n  - user: hi\n").IsValid);

    [Fact]
    public void An_event_not_emitted_check_needs_a_declared_event()
    {
        var scenario =
            "key: k\nconversation:\n  - user: hi\nchecks:\n  - { key: c, type: event-not-emitted, event: state.changed, data: { node: x } }\n";
        var undeclared = scenario.Replace("state.changed", "nope", StringComparison.Ordinal);

        Assert.True(Validate(scenario).IsValid);
        AssertProblem(Validate(undeclared), "checks[0].event", "nope");
        Assert.True(Validate(undeclared, offline: true).IsValid);
    }

    [Fact]
    public void A_cited_check_needs_sources_and_none_empty()
    {
        const string Head = "key: k\nconversation:\n  - user: hi\nchecks:\n";

        Assert.True(Validate(Head + "  - { key: c, type: cited, sources: [plans, faq] }\n").IsValid);
        AssertProblem(Validate(Head + "  - { key: c, type: cited, sources: [] }\n"), "checks[0].sources", "at least one");
        AssertProblem(Validate(Head + "  - { key: c, type: cited, sources: [plans, ' '] }\n"), "checks[0].sources[1]", "not empty");
        Assert.False(Validate(Head + "  - { key: c, type: cited }\n").IsValid);
    }

    [Fact]
    public void A_result_on_tool_called_is_read_and_its_patterns_are_checked()
    {
        const string Head = "key: k\nconversation:\n  - user: hi\nchecks:\n";

        Assert.True(
            Validate(
                Head + "  - key: c\n    type: tool-called\n    tool: get_order\n    result: { status: { $pattern: 'deliv' } }\n"
            ).IsValid
        );
        AssertProblem(
            Validate(Head + "  - key: c\n    type: tool-called\n    tool: get_order\n    result: { items: [{ $pattern: '([' }] }\n"),
            "checks[0].result.items[0].$pattern",
            "regular expression"
        );
    }

    [Fact]
    public void An_invalid_pattern_in_args_or_data_is_a_problem_at_its_path()
    {
        const string Head = "key: k\nconversation:\n  - user: hi\nchecks:\n";

        AssertProblem(
            Validate(Head + "  - key: c\n    type: tool-called\n    tool: get_order\n    args: { zip: { $pattern: '(' } }\n"),
            "checks[0].args.zip.$pattern",
            "regular expression"
        );
        AssertProblem(
            Validate(Head + "  - key: c\n    type: event\n    event: state.changed\n    data: { node: { $pattern: '*' } }\n"),
            "checks[0].data.node.$pattern",
            "regular expression"
        );
        AssertProblem(
            Validate(Head + "  - key: c\n    type: event-not-emitted\n    event: state.changed\n    data: { $pattern: '(' }\n"),
            "checks[0].data.$pattern",
            "regular expression"
        );
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

    [Fact]
    public void A_judge_check_needs_criteria()
    {
        AssertProblem(
            Validate("key: k\nconversation:\n  - user: hi\nchecks:\n  - { key: c, type: judge, criteria: '  ' }\n"),
            "checks[0].criteria",
            "needs 'criteria'"
        );
        Assert.True(Validate("key: k\nconversation:\n  - user: hi\nchecks:\n  - { key: c, type: judge, criteria: polite }\n").IsValid);
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
    [InlineData("type: judge", "Missing required field: criteria")]
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

    private static string WithChecks(string checks) => $"key: k\nconversation:\n  - user: hi\nchecks:\n{checks}";

    [Fact]
    public void Correct_judge_matchers_in_args_and_data_are_valid()
    {
        var result = Validate(
            WithChecks(
                """
                  - key: reason-fits
                    type: tool-called
                    tool: refund_order
                    args:
                      order_id: A-17
                      reason:
                        $judge:
                          description: "Says the item arrived damaged or broken"
                          examples: ["kettle arrived broken", { code: 1 }]
                      items:
                        - note: { $judge: { examples: [x] } }
                  - key: note-fits
                    type: event
                    event: state.changed
                    data: { $judge: { description: "anything about a refund" } }
                """
            )
        );

        Assert.Empty(result.Problems);
    }

    [Theory]
    [InlineData( /*lang=json*/
        "{ $judge: {} }",
        "checks[0].args.reason.$judge",
        "needs 'description' and/or 'examples'"
    )]
    [InlineData("{ $judge: text }", "checks[0].args.reason.$judge", "is an object")]
    [InlineData( /*lang=json*/
        "{ $judge: { description: '  ' } }",
        "checks[0].args.reason.$judge.description",
        "non-empty string"
    )]
    [InlineData( /*lang=json*/
        "{ $judge: { description: 3 } }",
        "checks[0].args.reason.$judge.description",
        "non-empty string"
    )]
    [InlineData( /*lang=json*/
        "{ $judge: { examples: [] } }",
        "checks[0].args.reason.$judge.examples",
        "non-empty list"
    )]
    [InlineData("{ $judge: { examples: x } }", "checks[0].args.reason.$judge.examples", "non-empty list")]
    [InlineData("{ $judge: { description: d, hint: 1 } }", "checks[0].args.reason.$judge", "no key 'hint'")]
    [InlineData("{ $judge: { description: d }, other: 1 }", "checks[0].args.reason", "only property")]
    public void A_malformed_judge_matcher_is_reported_at_its_path(string matcher, string path, string messagePart)
    {
        var result = Validate(
            WithChecks($"  - key: c\n    type: tool-called\n    tool: refund_order\n    args:\n      reason: {matcher}\n")
        );

        AssertProblem(result, path, messagePart);
    }

    [Fact]
    public void A_malformed_judge_matcher_in_event_data_is_reported_with_the_data_path()
    {
        var result = Validate(
            WithChecks("  - key: c\n    type: event\n    event: state.changed\n    data:\n      list: [ { $judge: {} } ]\n")
        );

        AssertProblem(result, "checks[0].data.list[0].$judge", "needs 'description' and/or 'examples'");
    }

    [Fact]
    public void A_judge_matcher_in_a_check_without_args_or_data_is_not_a_known_field()
    {
        var result = Validate(
            WithChecks(
                "  - key: c\n    type: tool-not-called\n    tool: refund_order\n    args: { reason: { $judge: { description: d } } }\n"
            )
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_judge_matcher_is_valid_in_a_tool_result_and_checked_there_too()
    {
        Assert.Empty(
            Validate(
                WithChecks(
                    "  - key: c\n    type: tool-called\n    tool: get_order\n    result: { note: { $judge: { description: d } }, id: { $pattern: '^A' } }\n"
                )
            ).Problems
        );
        AssertProblem(
            Validate(WithChecks("  - key: c\n    type: tool-called\n    tool: get_order\n    result: { note: { $judge: {} } }\n")),
            "checks[0].result.note.$judge",
            "needs 'description' and/or 'examples'"
        );
    }

    [Fact]
    public void A_judge_matcher_next_to_a_pattern_matcher_is_ambiguous_and_rejected()
    {
        var result = Validate(
            WithChecks(
                "  - key: c\n    type: tool-called\n    tool: get_order\n    args: { reason: { $judge: { description: d }, $pattern: x } }\n"
            )
        );

        AssertProblem(result, "checks[0].args.reason", "only property");
    }

    [Fact]
    public void A_judge_matcher_in_an_event_not_emitted_check_is_rejected_but_a_pattern_is_fine()
    {
        var judged = Validate(
            WithChecks(
                "  - key: c\n    type: event-not-emitted\n    event: state.changed\n    data: { note: { $judge: { description: d } } }\n"
            )
        );
        var patterned = Validate(
            WithChecks("  - key: c\n    type: event-not-emitted\n    event: state.changed\n    data: { note: { $pattern: x } }\n")
        );

        AssertProblem(judged, "checks[0].data", "not allowed in an event-not-emitted check");
        Assert.Empty(patterned.Problems);
    }
}
