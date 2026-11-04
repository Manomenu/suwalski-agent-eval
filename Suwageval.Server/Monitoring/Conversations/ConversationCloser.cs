using Suwageval.Contracts.Agents;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Monitoring.Conversations;

internal sealed partial class ConversationCloser(
    ConversationStore conversations,
    AgentStore agents,
    TimeProvider time,
    ILogger<ConversationCloser> logger
) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                await CloseIdleAsync(stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                LogFailed(error);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task CloseIdleAsync(CancellationToken cancellationToken)
    {
        foreach (var agentId in await conversations.AgentsWithOpenAsync(cancellationToken))
        {
            var agent = await agents.GetAsync(agentId.ToString(), cancellationToken);
            var closeAfter = agent?.Features.Monitoring is { } settings
                ? FeatureRules.CloseAfter(settings)
                : FeatureRules.DefaultCloseAfter;
            await conversations.CloseIdleAsync(agentId, time.GetUtcNow() - closeAfter, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Closing idle conversations failed; trying again at the next look")]
    private partial void LogFailed(Exception error);
}
