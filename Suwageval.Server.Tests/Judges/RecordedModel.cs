using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Judges;
using Suwageval.Server.LanguageModel;

namespace Suwageval.Server.Tests.Judges;

[SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "The recorded model and the clients over it hold nothing but memory."
)]
internal sealed class RecordedModel(Func<CancellationToken, Task<string>> answer) : IChatClient
{
    private readonly List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> requests = [];

    public IReadOnlyList<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Requests
    {
        get
        {
            lock (requests)
            {
                return [.. requests];
            }
        }
    }

    public static RecordedModel Answering(string file)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Judges", "Answers", file));
        return new RecordedModel(_ => Task.FromResult(text));
    }

    public static Judge JudgeAnswering(string file) => Answering(file).Judge();

    public static Judge JudgeFailing(Exception error) => Failing(error).Judge();

    public static UserSimulator SimulatorAnswering(params string[] files) => Sequence(files).Simulator();

    public static UserSimulator SimulatorFailing(Exception error) => Failing(error).Simulator();

    public static RecordedModel Sequence(params string[] files)
    {
        var answers = files.Select(file => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Judges", "Answers", file))).ToArray();
        var next = -1;
        return new RecordedModel(_ => Task.FromResult(answers[Math.Min(Interlocked.Increment(ref next), answers.Length - 1)]));
    }

    public static RecordedModel Failing(Exception error) => new(_ => Task.FromException<string>(error));

    public Judge Judge(int maxConcurrency = 8) => new(Clients(maxConcurrency), Options.Create(new JudgeOptions { Model = "judge" }));

    public ArgumentJudge ArgumentJudge(int maxConcurrency = 8) =>
        new(Clients(maxConcurrency), Options.Create(new JudgeOptions { Model = "judge" }));

    public UserSimulator Simulator(double temperature = 0.7) =>
        new(Clients(), Options.Create(new SimulationOptions { Temperature = temperature }));

    public UserDescriber Describer() => new(Clients());

    public ModelClients Clients(int maxConcurrency = 8) =>
        new(
            new ModelsOptions
            {
                ["fallback"] = new ModelOptions
                {
                    BaseUrl = "http://model.invalid",
                    Model = "recorded",
                    MaxConcurrency = maxConcurrency,
                },
            },
            _ => this
        );

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        lock (requests)
        {
            requests.Add(([.. messages], options));
        }
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, await answer(cancellationToken)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
