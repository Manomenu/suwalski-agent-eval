using Suwageval.Contracts;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Scenarios;

/// <summary>What a request about scenarios must satisfy, checked where it enters. The rules <c>suwageval validate</c> applies to the same fields.</summary>
internal static class ScenarioValidation
{
    public static List<FieldError> Validate(IReadOnlyList<ScenarioInput> scenarios)
    {
        var errors = new List<FieldError>();
        var keys = new HashSet<string>();
        for (var i = 0; i < scenarios.Count; i++)
        {
            var scenario = scenarios[i];
            var path = $"scenarios[{i}]";
            if (!Kebab.IsValid(scenario.Key))
            {
                errors.Add(
                    new FieldError(
                        $"{path}.key",
                        $"The scenario key '{scenario.Key}' is not lowercase kebab-case (like 'refund-happy-path')."
                    )
                );
            }
            else if (!keys.Add(scenario.Key))
            {
                errors.Add(new FieldError($"{path}.key", $"The scenario key '{scenario.Key}' is used twice in the request."));
            }
            AddTagErrors(errors, scenario.Tags, $"{path}.tags");
            if (scenario.Samples < 1)
            {
                errors.Add(new FieldError($"{path}.samples", "samples is at least 1."));
            }
            if (scenario.Continuation.Turns < 1)
            {
                errors.Add(new FieldError($"{path}.continuation.turns", "turns is at least 1."));
            }
            if (scenario.Conversation.Count == 0)
            {
                errors.Add(new FieldError($"{path}.conversation", "The conversation has no items; the agent needs a message to answer."));
            }
        }
        return errors;
    }

    public static List<FieldError> Validate(TagChangeRequest request)
    {
        var errors = new List<FieldError>();
        for (var i = 0; i < request.ScenarioKeys.Count; i++)
        {
            if (!Kebab.IsValid(request.ScenarioKeys[i]))
            {
                errors.Add(
                    new FieldError($"scenarioKeys[{i}]", $"The scenario key '{request.ScenarioKeys[i]}' is not lowercase kebab-case.")
                );
            }
        }
        AddTagErrors(errors, request.Add, "add");
        AddTagErrors(errors, request.Remove, "remove");
        return errors;
    }

    private static void AddTagErrors(List<FieldError> errors, IReadOnlyList<string> tags, string path)
    {
        for (var i = 0; i < tags.Count; i++)
        {
            if (!Kebab.IsValid(tags[i]))
            {
                errors.Add(new FieldError($"{path}[{i}]", $"The tag '{tags[i]}' is not lowercase kebab-case (like 'refund-flow')."));
            }
        }
    }
}
