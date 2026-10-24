using Suwageval.Contracts.Agents;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Monitoring.Conversations;

/// <summary>
/// Closes the conversations no turn came to for their agent's <c>features.monitoring.closeAfter</c> (30 minutes by default; the default too
/// for an agent whose monitoring was switched off since). A hosted service of the server, like the run worker: no component of its own.
/// </summary>
internal sealed partial class ConversationCloser(
    ConversationStore conversations,
    AgentStore agents,
    TimeProvider time,
    ILogger<ConversationCloser> logger
) : BackgroundService
{
    /// <summary>How often the open conversations are looked at.</summary>
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

    /// <summary>One look: every agent with an open conversation, each with its own closeAfter.</summary>
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
