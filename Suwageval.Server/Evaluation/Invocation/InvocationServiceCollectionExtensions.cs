using Microsoft.Extensions.Options;

namespace Suwageval.Server.Evaluation.Invocation;

internal static class InvocationServiceCollectionExtensions
{
    public static IServiceCollection AddInvocation(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<InvocationOptions>()
            .Bind(configuration.GetSection(InvocationOptions.Section))
            .Validate(options => options.TimeoutSeconds > 0, "Invocation:TimeoutSeconds must be above 0")
            .ValidateOnStart();
        services
            .AddHttpClient<AgentClient>(
                (provider, client) =>
                    client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<InvocationOptions>>().Value.TimeoutSeconds)
            )
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
        return services;
    }
}
