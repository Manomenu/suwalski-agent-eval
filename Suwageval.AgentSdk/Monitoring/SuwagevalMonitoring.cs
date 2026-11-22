using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Suwageval.AgentSdk.Monitoring;

public static class SuwagevalMonitoring
{
    public const string HttpClientName = "Suwageval.Monitoring";

    public const int QueueCapacity = 1000;

    public static IServiceCollection AddSuwagevalMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(MonitoringOptions.Section);
        if (string.IsNullOrWhiteSpace(section[nameof(MonitoringOptions.Url)]))
        {
            services.AddSingleton<IConversationRecorder>(new NoConversationRecorder());
            return services;
        }
        services
            .AddOptions<MonitoringOptions>()
            .Bind(section)
            .Validate(
                options => Uri.TryCreate(options.Url, UriKind.Absolute, out var url) && url.Scheme is "http" or "https",
                "Suwageval:Url is an absolute http or https address"
            )
            .Validate(options => !string.IsNullOrWhiteSpace(options.Agent), "Suwageval:Agent names the agent (name in its agent.yaml)")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Environment), "Suwageval:Environment names where the agent runs")
            .Validate(
                options => string.IsNullOrEmpty(options.AccessClientId) == string.IsNullOrEmpty(options.AccessClientSecret),
                "Suwageval:AccessClientId and Suwageval:AccessClientSecret are set together"
            )
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(
            Channel.CreateBounded<MonitoredTurn>(
                new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true }
            )
        );
        services.AddSingleton<IConversationRecorder>(provider => new ConversationRecorder(
            provider.GetRequiredService<Channel<MonitoredTurn>>(),
            provider.GetRequiredService<TimeProvider>()
        ));
        services.AddHttpClient(HttpClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitoringOptions>>().Value;
            client.BaseAddress = new Uri(
                $"{(options.Url ?? throw new InvalidOperationException("This registration runs only with Suwageval:Url set, and validation on start refuses a bad one.")).TrimEnd('/')}/api/"
            );
            client.Timeout = TimeSpan.FromSeconds(10);
            if (!string.IsNullOrEmpty(options.AccessClientId))
            {
                client.DefaultRequestHeaders.Add("CF-Access-Client-Id", options.AccessClientId);
                client.DefaultRequestHeaders.Add("CF-Access-Client-Secret", options.AccessClientSecret);
            }
        });
        services.AddHostedService(provider => new MonitoringSender(
            provider.GetRequiredService<Channel<MonitoredTurn>>(),
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<Invocation.IAgentContractSource>(),
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitoringOptions>>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MonitoringSender>>(),
            provider.GetRequiredService<TimeProvider>()
        ));
        return services;
    }
}
