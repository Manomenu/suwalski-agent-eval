namespace Suwageval.Server.Monitoring.Conversations;

internal static class ConversationServiceCollectionExtensions
{
    public static IServiceCollection AddConversations(this IServiceCollection services, bool startCloser)
    {
        services.AddSingleton<ConversationStore>();
        services.AddSingleton(TimeProvider.System);
        if (startCloser)
        {
            services.AddHostedService<ConversationCloser>();
        }
        return services;
    }
}
