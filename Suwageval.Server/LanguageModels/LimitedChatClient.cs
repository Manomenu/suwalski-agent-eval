using Microsoft.Extensions.AI;

namespace Suwageval.Server.LanguageModels;

/// <summary>
/// A chat client that lets only so many calls through at once and gives each a time limit. One per configured model and
/// shared by everything that calls that model, so the limit is global to the model (the judge today, the user simulator later).
/// </summary>
/// <remarks>Only <see cref="GetResponseAsync"/> is limited and offered: nothing here streams yet.</remarks>
internal sealed class LimitedChatClient(IChatClient inner, int maxConcurrency, TimeSpan timeout) : DelegatingChatClient(inner)
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
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(timeout);
            try
            {
                return await base.GetResponseAsync(messages, options, limit.Token);
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
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
