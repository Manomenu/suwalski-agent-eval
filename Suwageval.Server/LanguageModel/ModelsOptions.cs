using Microsoft.Extensions.Options;

namespace Suwageval.Server.LanguageModel;

internal sealed class ModelsOptions : Dictionary<string, ModelOptions>
{
    public const string Section = "Models";

    public ModelsOptions()
        : base(StringComparer.OrdinalIgnoreCase) { }

    internal sealed class ModelsOptionsValidator : IValidateOptions<ModelsOptions>
    {
        public ValidateOptionsResult Validate(string? name, ModelsOptions options)
        {
            var problems = options.Select(model => model.Value.Problem(model.Key)).OfType<string>().ToList();
            return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
        }
    }
}
