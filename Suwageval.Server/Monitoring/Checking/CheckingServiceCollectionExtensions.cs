namespace Suwageval.Server.Monitoring.Checking;

internal static class CheckingServiceCollectionExtensions
{
    /// <summary>The monitoring checks' runner and, unless <paramref name="startWorker"/> is false (the OpenAPI build, the tests), its worker.</summary>
    public static IServiceCollection AddChecking(this IServiceCollection services, bool startWorker)
    {
        services.AddSingleton<CheckingStore>().AddSingleton<ConversationChecker>().AddSingleton<MonitoringTrendStore>();
        if (startWorker)
        {
            services.AddHostedService<MonitoringWorker>();
        }
        return services;
    }
}
