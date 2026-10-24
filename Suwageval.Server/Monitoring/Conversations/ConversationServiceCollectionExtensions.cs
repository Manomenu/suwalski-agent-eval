namespace Suwageval.Server.Monitoring.Conversations;

internal static class ConversationServiceCollectionExtensions
{
    /// <summary>The conversations' store and, unless <paramref name="startCloser"/> is false (the OpenAPI build, the tests), their closer.</summary>
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
