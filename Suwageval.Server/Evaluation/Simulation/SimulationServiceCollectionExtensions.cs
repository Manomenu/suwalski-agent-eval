using Suwageval.Server.LanguageModel;

namespace Suwageval.Server.Evaluation.Simulation;

internal static class SimulationServiceCollectionExtensions
{
    public static IServiceCollection AddSimulation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModels(configuration);
        services
            .AddOptions<SimulationOptions>()
            .Bind(configuration.GetSection(SimulationOptions.Section))
            .Validate(options => options.Temperature is >= 0 and <= 2, "Simulation:Temperature is from 0 to 2")
            .ValidateOnStart();
        services.AddSingleton<UserSimulator>();
        services.AddSingleton<UserDescriber>();
        return services;
    }
}
