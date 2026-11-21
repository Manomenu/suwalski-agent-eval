using Microsoft.Extensions.AI;

namespace Suwageval.Server.LanguageModel;

internal sealed class LimitedChatClient(IChatClient inner, int maxConcurrency, TimeSpan timeout, TimeProvider timeProvider)
    : DelegatingChatClient(inner)
{
    private readonly SemaphoreSlim slots = new(maxConcurrency, maxConcurrency);

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        await slots.WaitAsync(cancellationToken);
        try
        {
            using var expiry = new CancellationTokenSource(timeout, timeProvider);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiry.Token);
            try
            {
                return await base.GetResponseAsync(messages, options, limit.Token);
            }
            catch (OperationCanceledException) when (expiry.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The model did not answer within {timeout.TotalSeconds:0.##} seconds.");
            }
        }
        finally
        {
            slots.Release();
        }
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException("Streaming is not used yet, so it has no limit; add one with the first caller that streams.");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            slots.Dispose();
        }
        base.Dispose(disposing);
    }
}
