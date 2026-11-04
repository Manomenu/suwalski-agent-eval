using Microsoft.Extensions.Options;

namespace Suwageval.Server.LanguageModel;

internal static class ModelsServiceCollectionExtensions
{
    public static IServiceCollection AddModels(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(service => service.ServiceType == typeof(ModelClients)))
        {
            return services;
        }
        services.AddOptions<ModelsOptions>().Bind(configuration.GetSection(ModelsOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ModelsOptions>, ModelsOptions.ModelsOptionsValidator>();
        services.AddSingleton(provider => new ModelClients(
            provider.GetRequiredService<IOptions<ModelsOptions>>().Value,
            ModelClients.OpenAiCompatible
        ));
        return services;
    }
}
