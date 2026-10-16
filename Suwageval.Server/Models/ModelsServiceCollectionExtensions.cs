using Microsoft.Extensions.Options;

namespace Suwageval.Server.Models;

internal static class ModelsServiceCollectionExtensions
{
    /// <summary>The configured models and their clients; a model with a missing or malformed setting stops the server at start.</summary>
    public static IServiceCollection AddModels(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ModelsOptions>().Bind(configuration.GetSection(ModelsOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ModelsOptions>, ModelsOptions.Validator>();
        services.AddSingleton(provider => new ModelClients(
            provider.GetRequiredService<IOptions<ModelsOptions>>().Value,
            ModelClients.OpenAiCompatible
        ));
        return services;
    }
}
