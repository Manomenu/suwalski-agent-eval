using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Verdicts;
using static Suwageval.Server.Tests.Evaluation.Checks.Items;

namespace Suwageval.Server.Tests.Evaluation.Checks;

public sealed class CheckTypesTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly Dictionary<Type, CheckInput> Samples = new CheckInput[]
    {
        new ToolCalledCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Tool = "t",
        },
        new ToolNotCalledCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Tool = "t",
        },
        new EventCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Event = "e",
        },
        new EventNotEmittedCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Event = "e",
        },
        new CitedCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Sources = ["doc"],
        },
        new TextCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Contains = ["x"],
        },
        new MessageCountCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Max = 1,
        },
        new JudgeCheck
        {
            Key = "a",
            ScenarioKey = "s",
            Criteria = "polite",
        },
    }.ToDictionary(check => check.GetType());

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This guard has to see a check type nobody listed yet.")]
    private static Type[] CheckTypes() =>
        [.. typeof(CheckInput).Assembly.GetTypes().Where(type => type is { IsAbstract: false } && type.IsSubclassOf(typeof(CheckInput)))];

    private static Dictionary<Type, string> Discriminators() =>
        typeof(CheckInput)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .ToDictionary(a => a.DerivedType, a => (string)a.TypeDiscriminator!);

    public static TheoryData<string> TypeNames() => [.. CheckTypes().Select(type => type.Name)];

    private static Type TypeNamed(string name) => CheckTypes().Single(type => type.Name == name);

    [Fact]
    public void Every_check_type_has_a_discriminator_and_a_sample()
    {
        var types = CheckTypes();

        Assert.Equal(types.Select(type => type.Name).Order(), Discriminators().Keys.Select(type => type.Name).Order());
        Assert.True(
            types.All(Samples.ContainsKey) && Samples.Count == types.Length,
            $"add a sample to {nameof(Samples)} for: {string.Join(", ", types.Where(type => !Samples.ContainsKey(type)).Select(type => type.Name))}"
        );
    }

    [Theory]
    [Trait("criterion", "AC-message-count-check-7")]
    [MemberData(nameof(TypeNames))]
    public void The_scenario_format_documents_the_type(string typeName)
    {
        var discriminator = Discriminators()[TypeNamed(typeName)];
        var doc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "scenario-format.md"));

        Assert.Contains($"| `{discriminator}` |", doc, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-message-count-check-7")]
    [MemberData(nameof(TypeNames))]
    public void The_plugin_reference_skill_names_the_type(string typeName)
    {
        var discriminator = Discriminators()[TypeNamed(typeName)];
        var skill = File.ReadAllText(Path.Combine(RepoRoot, "claude-plugin", "skills", "suwageval", "SKILL.md"));

        Assert.Contains($"`{discriminator}`", skill, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-check-options-8")]
    [InlineData("docs/scenario-format.md", "`min` (from 0, only beside `max`)")]
    [InlineData("docs/scenario-format.md", "`exactly`\n(from 1, alone)")]
    [InlineData("docs/scenario-format.md", "`last: true` compares only the last event")]
    [InlineData("docs/scenario-format.md", "`contains` and `notContains` take one text or a list of texts")]
    [InlineData("claude-plugin/skills/suwageval/SKILL.md", "`max: 1` (also `min`, `exactly`) counts the matching events")]
    [InlineData("claude-plugin/skills/suwageval/SKILL.md", "`last: true`\n  with `data` compares only the last event")]
    [InlineData("claude-plugin/skills/suwageval/SKILL.md", "`notContains: [a, b, c]` (and `contains`)")]
    public void The_docs_name_the_event_and_text_check_options(string path, string option)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, path)).ReplaceLineEndings("\n");

        Assert.Contains(option, text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void The_shared_rules_accept_a_valid_check_and_reject_a_broken_one(string typeName)
    {
        var sample = Samples[TypeNamed(typeName)];

        Assert.Empty(CheckRules.Problems(sample, "checks[0]"));
        Assert.NotEmpty(CheckRules.Problems(sample with { PassThreshold = 0 }, "checks[0]"));
    }

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void The_server_evaluates_the_type_or_hands_it_to_the_model(string typeName)
    {
        var sample = Samples[TypeNamed(typeName)];

        if (CheckEvaluator.JudgedByModel(sample))
        {
            Assert.IsType<JudgeCheck>(sample);
            return;
        }
        var verdict = CheckEvaluator.Evaluate(sample, Turn());

        Assert.NotEmpty(verdict.Message);
    }

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void The_web_shows_the_type(string typeName)
    {
        var discriminator = Discriminators()[TypeNamed(typeName)];
        var definition = File.ReadAllText(Path.Combine(RepoRoot, "Suwageval.Web", "src", "evaluation", "checks", "definition.ts"));

        Assert.Contains($"\"{discriminator}\"", definition, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Suwageval.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("Suwageval.slnx not found above " + AppContext.BaseDirectory);
    }
}
