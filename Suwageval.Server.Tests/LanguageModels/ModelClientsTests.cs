using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Judges;
using Suwageval.Server.LanguageModels;
using Suwageval.Server.Tests.Evaluation.Checks;
using Suwageval.Server.Tests.Judges;

namespace Suwageval.Server.Tests.LanguageModels;

/// <summary>The pool of calls per model: its limit, its time limit, that it is one per model, and the configuration that makes it.</summary>
[SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "The recorded model and the clients over it hold nothing but memory."
)]
public sealed class ModelClientsTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly ChatMessage[] Question = [new(ChatRole.User, "?")];

    /// <summary>A model that counts its calls in flight and holds each one for a moment.</summary>
    private sealed class Counting
    {
        private int inFlight;
        private int peak;

        public int Peak => Volatile.Read(ref peak);

        public RecordedModel Model { get; }

        public Counting()
        {
            Model = new RecordedModel(async cancellation =>
            {
                var now = Interlocked.Increment(ref inFlight);
                int seen;
                while (now > (seen = Volatile.Read(ref peak)) && Interlocked.CompareExchange(ref peak, now, seen) != seen) { }
                await Task.Delay(30, cancellation);
                Interlocked.Decrement(ref inFlight);
                return "{}";
            });
        }
    }

    [Fact]
    public async Task Twenty_calls_through_eight_slots_never_have_more_than_eight_in_flight()
    {
        var counting = new Counting();
        var client = counting.Model.Clients(maxConcurrency: 8).Get("fallback");

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.GetResponseAsync(Question, cancellationToken: Cancellation)));

        Assert.Equal(8, counting.Peak);
    }

    [Fact]
    public async Task The_pool_of_a_model_is_shared_by_everything_that_asks_for_it()
    {
        var counting = new Counting();
        var clients = counting.Model.Clients(maxConcurrency: 3);

        Assert.Same(clients.Get("fallback"), clients.Get("FALLBACK"));

        // Two judges (two runs, say) on the same clients compete for the same three slots.
        var options = Options.Create(new JudgeOptions { Model = "judge" });
        var first = new Judge(clients, options);
        var second = new Judge(clients, options);
        var check = new Contracts.Checks.JudgeCheck
        {
            Key = "k",
            ScenarioKey = "s",
            Criteria = "c",
        };
        var trace = Items.Turn(Items.Say("hi"));
        var calls = Enumerable.Range(0, 12).Select(i => (i % 2 == 0 ? first : second).JudgeAsync(check, [], trace, Cancellation));

        await Task.WhenAll(calls);

        Assert.Equal(3, counting.Peak);
    }

    [Fact]
    public async Task A_call_waiting_for_a_slot_stops_when_cancelled()
    {
        var model = new RecordedModel(async cancellation =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return "";
        });
        var client = model.Clients(maxConcurrency: 1).Get("fallback");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var holding = client.GetResponseAsync(Question, cancellationToken: source.Token);
        var waiting = client.GetResponseAsync(Question, cancellationToken: source.Token);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => holding);
    }

    [Fact]
    public async Task A_call_that_takes_longer_than_the_limit_is_a_timeout_and_gives_its_slot_back()
    {
        var model = new RecordedModel(async cancellation =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return "";
        });
        using var limited = new LimitedChatClient(model, 1, TimeSpan.FromMilliseconds(50));

        var first = await Assert.ThrowsAsync<TimeoutException>(() => limited.GetResponseAsync(Question, cancellationToken: Cancellation));
        // The one slot is free again: a second call is let in (and times out the same way) instead of waiting forever.
        await Assert.ThrowsAsync<TimeoutException>(() => limited.GetResponseAsync(Question, cancellationToken: Cancellation));

        Assert.Contains("did not answer", first.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_model_is_an_error()
    {
        var clients = new RecordedModel(_ => Task.FromResult("")).Clients();

        Assert.Throws<InvalidOperationException>(() => clients.Get("nope"));
    }

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddJudges(configuration).BuildServiceProvider();
    }

    private static string? ValidationFailure(Dictionary<string, string?> settings)
    {
        using var services = Services(settings);
        try
        {
            _ = services.GetRequiredService<IOptions<ModelsOptions>>().Value;
            _ = services.GetRequiredService<IOptions<JudgeOptions>>().Value;
            return null;
        }
        catch (OptionsValidationException error)
        {
            return string.Join(' ', error.Failures);
        }
    }

    [Fact]
    public void A_model_gets_eight_slots_and_a_timeout_unless_configured()
    {
        using var services = Services(
            new()
            {
                ["Models:judge:BaseUrl"] = "http://vllm.example/v",
                ["Models:judge:Model"] = "qwen38",
                ["Judge:Model"] = "judge",
            }
        );

        var model = services.GetRequiredService<IOptions<ModelsOptions>>().Value["judge"];

        Assert.Equal((8, 120), (model.MaxConcurrency, model.TimeoutSeconds));
        Assert.True(services.GetRequiredService<Judge>().IsConfigured);
    }

    [Fact]
    public void Every_role_without_a_model_of_its_own_takes_the_fallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Judge:Model"] = "judge",
                    ["Models:fallback:BaseUrl"] = "http://fallback.example/v",
                    ["Models:fallback:Model"] = "small",
                    ["Models:simulator:BaseUrl"] = "http://simulator.example/v",
                    ["Models:simulator:Model"] = "big",
                }
            )
            .Build();
        using var services = new ServiceCollection().AddJudges(configuration).AddSimulation(configuration).BuildServiceProvider();

        Assert.Equal("small", services.GetRequiredService<Judge>().ModelName);
        Assert.Equal("simulator", services.GetRequiredService<UserSimulator>().ModelEntry);
        Assert.Equal("fallback", services.GetRequiredService<UserDescriber>().ModelEntry);
    }

    [Fact]
    public void A_judge_naming_a_model_that_is_not_configured_is_valid_and_not_configured()
    {
        Assert.Null(ValidationFailure(new() { ["Judge:Model"] = "judge" }));
        using var services = Services(new() { ["Judge:Model"] = "judge" });
        Assert.False(services.GetRequiredService<Judge>().IsConfigured);
    }

    [Fact]
    public void No_models_and_no_judge_is_valid_and_the_judge_is_not_configured()
    {
        Assert.Null(ValidationFailure([]));
        using var services = Services([]);
        Assert.False(services.GetRequiredService<Judge>().IsConfigured);
    }

    [Theory]
    [InlineData("Models:judge:BaseUrl", "not a url", "BaseUrl")]
    [InlineData("Models:judge:Model", "", "Model is required")]
    [InlineData("Models:judge:MaxConcurrency", "0", "MaxConcurrency")]
    [InlineData("Models:judge:TimeoutSeconds", "0", "TimeoutSeconds")]
    public void A_missing_or_malformed_setting_fails_the_start(string key, string value, string expected)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Models:judge:BaseUrl"] = "http://vllm.example/v",
            ["Models:judge:Model"] = "qwen38",
            ["Judge:Model"] = "judge",
        };
        settings[key] = value;

        var failure = ValidationFailure(settings);

        Assert.Contains(expected, failure, StringComparison.Ordinal);
    }
}
