using Suwageval.Server.Evaluation.Invocation;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Judges;

namespace Suwageval.Server.Evaluation.Runs;

internal static class RunServiceCollectionExtensions
{
    public static IServiceCollection AddRuns(this IServiceCollection services, IConfiguration configuration, bool startWorker)
    {
        services.AddInvocation(configuration);
        services.AddJudges(configuration);
        services.AddSimulation(configuration);
        services
            .AddOptions<RunOptions>()
            .Bind(configuration.GetSection(RunOptions.Section))
            .Validate(options => options.MaxParallelScenarios > 0, "Runs:MaxParallelScenarios must be above 0")
            .Validate(options => options.PollInterval > TimeSpan.Zero, "Runs:PollInterval must be above 0")
            .ValidateOnStart();
        services.AddSingleton<RunStore>().AddSingleton<RunExecutor>().AddSingleton<RunRechecker>();
        if (startWorker)
        {
            services.AddHostedService<RunWorker>();
        }
        return services;
    }
}
