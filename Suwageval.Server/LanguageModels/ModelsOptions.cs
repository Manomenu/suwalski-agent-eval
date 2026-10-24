using Microsoft.Extensions.Options;

namespace Suwageval.Server.LanguageModels;

/// <summary>The <c>Models</c> section: the models by name. Empty is fine until something names one.</summary>
internal sealed class ModelsOptions : Dictionary<string, ModelOptions>
{
    public const string Section = "Models";

    public ModelsOptions()
        : base(StringComparer.OrdinalIgnoreCase) { }

    /// <summary>Every model must be complete, so a missing or malformed setting stops the server at start.</summary>
    internal sealed class Validator : IValidateOptions<ModelsOptions>
    {
        public ValidateOptionsResult Validate(string? name, ModelsOptions options)
        {
            var problems = options.Select(model => model.Value.Problem(model.Key)).OfType<string>().ToList();
            return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
        }
    }
}
