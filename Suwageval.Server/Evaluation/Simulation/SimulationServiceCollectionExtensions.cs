using Suwageval.Server.LanguageModels;

namespace Suwageval.Server.Evaluation.Simulation;

internal static class SimulationServiceCollectionExtensions
{
    /// <summary>The user simulator and its settings (with the models it needs); a malformed setting stops the server at start.</summary>
    public static IServiceCollection AddSimulation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModels(configuration);
        services
            .AddOptions<SimulationOptions>()
            .Bind(configuration.GetSection(SimulationOptions.Section))
            .Validate(options => options.Temperature is >= 0 and <= 2, "Simulation:Temperature is from 0 to 2")
            .ValidateOnStart();
        services.AddSingleton<UserSimulator>();
        return services;
    }
}
