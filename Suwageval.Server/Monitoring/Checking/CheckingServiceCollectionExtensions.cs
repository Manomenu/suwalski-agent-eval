namespace Suwageval.Server.Monitoring.Checking;

internal static class CheckingServiceCollectionExtensions
{
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
