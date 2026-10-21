using Suwageval.Server.Invocation;
using Suwageval.Server.Judges;

namespace Suwageval.Server.Runs;

internal static class RunServiceCollectionExtensions
{
    /// <summary>
    /// The run store, the executor, the agent client and the judge; with <paramref name="startWorker"/> also the worker that takes runs
    /// from the queue (off while the build writes the OpenAPI document, which touches no database).
    /// </summary>
    public static IServiceCollection AddRuns(this IServiceCollection services, IConfiguration configuration, bool startWorker)
    {
        services.AddInvocation(configuration);
        services.AddJudges(configuration);
        services
            .AddOptions<RunOptions>()
            .Bind(configuration.GetSection(RunOptions.Section))
            .Validate(options => options.MaxParallelScenarios > 0, "Runs:MaxParallelScenarios must be above 0")
            .Validate(options => options.PollInterval > TimeSpan.Zero, "Runs:PollInterval must be above 0")
            .ValidateOnStart();
        services.AddSingleton<RunStore>().AddSingleton<RunGroupStore>().AddSingleton<RunExecutor>().AddSingleton<RunRechecker>();
        if (startWorker)
        {
            services.AddHostedService<RunWorker>();
        }
        return services;
    }
}
