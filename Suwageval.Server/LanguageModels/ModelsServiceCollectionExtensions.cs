using Microsoft.Extensions.Options;

namespace Suwageval.Server.LanguageModels;

internal static class ModelsServiceCollectionExtensions
{
    /// <summary>
    /// The configured models and their clients; a model with a missing or malformed setting stops the server at start. Every feature that
    /// calls a model asks for this; it registers them once, so a model two features name has one client and one limit of calls at once.
    /// </summary>
    public static IServiceCollection AddModels(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(service => service.ServiceType == typeof(ModelClients)))
        {
            return services;
        }
        services.AddOptions<ModelsOptions>().Bind(configuration.GetSection(ModelsOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ModelsOptions>, ModelsOptions.Validator>();
        services.AddSingleton(provider => new ModelClients(
            provider.GetRequiredService<IOptions<ModelsOptions>>().Value,
            ModelClients.OpenAiCompatible
        ));
        return services;
    }
}
