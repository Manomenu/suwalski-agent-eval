using Suwageval.Cli.Files;
using Suwageval.Cli.Sweep;

namespace Suwageval.Cli.Tests.Sweep;

public sealed class VariantSweepTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly SweepSettings Settings = new()
    {
        VariantsDir = "setups",
        Reconfigure = "reconfigure {variant} {group}",
        Before = "before {variant}",
        After = "after {variant}",
        Restore = "restore",
    };

    private sealed class Fakes
    {
        private int reads;

        public List<string> Log { get; } = [];

        /// <summary>The label the agent serves after a reconfigure; a variant missing here keeps the previous label.</summary>
        public Dictionary<string, string> Labels { get; } = [];

        public string? Label { get; set; } = "default";

        public Func<string, int> ExitOf { get; set; } = _ => 0;

        public Func<int, string?>? LabelOnRead { get; set; }

        public Task<int> Shell(string commandLine, TextWriter output, CancellationToken cancellationToken)
        {
            Log.Add(commandLine);
            if (commandLine.StartsWith("reconfigure ", StringComparison.Ordinal) && ExitOf(commandLine) == 0)
            {
                Label = Labels.GetValueOrDefault(commandLine.Split(' ')[1], Label!);
            }
            return Task.FromResult(ExitOf(commandLine));
        }

        public Task<string?> ReadLabel(CancellationToken cancellationToken) => Task.FromResult(LabelOnRead?.Invoke(reads++) ?? Label);

        public Task<VariantOutcome> Run(CancellationToken cancellationToken)
        {
            Log.Add("run@" + Label);
            return Task.FromResult(new VariantOutcome("", Guid.NewGuid(), "ok", false));
        }

        public VariantSweep Sweep(TimeSpan? wait = null) =>
            new(Settings, Shell, ReadLabel, Run, TextWriter.Null, wait ?? TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task Each_variant_gets_before_reconfigure_run_and_after_with_the_placeholders_filled_and_restore_comes_last()
    {
        var fakes = new Fakes();
        fakes.Labels["a"] = "a";
        fakes.Labels["b"] = "b";

        var result = await fakes.Sweep().RunAsync(["a", "b"], "grp", Cancellation);

        Assert.False(result.Failed);
        Assert.Equal(
            ["before a", "reconfigure a grp", "run@a", "after a", "before b", "reconfigure b grp", "run@b", "after b", "restore"],
            fakes.Log
        );
    }

    [Fact]
    public async Task A_label_that_never_shows_up_fails_that_variant_only_and_after_and_restore_still_run()
    {
        var fakes = new Fakes();
        fakes.Labels["b"] = "b";

        var result = await fakes.Sweep(TimeSpan.FromMilliseconds(60)).RunAsync(["a", "b"], "grp", Cancellation);

        Assert.True(result.Failed);
        Assert.Contains("label is 'default', not 'a'", result.Outcomes[0].Summary, StringComparison.Ordinal);
        Assert.False(result.Outcomes[1].Failed);
        Assert.DoesNotContain("run@default", fakes.Log);
        Assert.Contains("after a", fakes.Log);
        Assert.Equal("restore", fakes.Log[^1]);
    }

    [Fact]
    public async Task A_label_that_shows_up_after_a_few_reads_is_waited_for()
    {
        var fakes = new Fakes { LabelOnRead = read => read < 3 ? null : "a" };

        var result = await fakes.Sweep().RunAsync(["a"], "grp", Cancellation);

        Assert.False(result.Failed);
    }

    [Fact]
    public async Task A_contract_that_cannot_be_read_is_reported_as_such()
    {
        var fakes = new Fakes { Label = null };

        var result = await fakes.Sweep(TimeSpan.FromMilliseconds(30)).RunAsync(["a"], "grp", Cancellation);

        Assert.Contains("could not be read", result.Outcomes.Single().Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_command_is_a_row_the_sweep_goes_on_and_restores()
    {
        var fakes = new Fakes { ExitOf = command => command == "reconfigure a grp" ? 3 : 0 };
        fakes.Labels["b"] = "b";

        var result = await fakes.Sweep().RunAsync(["a", "b"], "grp", Cancellation);

        Assert.True(result.Failed);
        Assert.Equal("reconfigure exited 3", result.Outcomes[0].Summary);
        Assert.False(result.Outcomes[1].Failed);
        Assert.Equal(["after a", "before b"], fakes.Log.Skip(2).Take(2));
        Assert.Equal("restore", fakes.Log[^1]);
    }

    [Fact]
    public async Task A_failing_before_skips_the_reconfigure_but_after_still_runs_and_the_backend_is_not_restored()
    {
        var fakes = new Fakes { ExitOf = command => command.StartsWith("before", StringComparison.Ordinal) ? 1 : 0 };

        var result = await fakes.Sweep().RunAsync(["a"], "grp", Cancellation);

        Assert.Equal(["before a", "after a"], fakes.Log);
        Assert.Equal("before exited 1", result.Outcomes.Single().Summary);
    }

    [Fact]
    public async Task Ctrl_C_during_a_run_stops_the_sweep_but_after_and_restore_run()
    {
        using var interrupt = new CancellationTokenSource();
        var fakes = new Fakes();
        fakes.Labels["a"] = "a";
        var sweep = new VariantSweep(
            Settings,
            fakes.Shell,
            fakes.ReadLabel,
            async token =>
            {
                await interrupt.CancelAsync();
                token.ThrowIfCancellationRequested();
                return await fakes.Run(token);
            },
            TextWriter.Null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(5)
        );

        var result = await sweep.RunAsync(["a", "b"], "grp", interrupt.Token);

        Assert.True(result.Interrupted);
        Assert.Equal("a", Assert.Single(result.Outcomes).Variant);
        Assert.Equal(["after a", "restore"], fakes.Log.TakeLast(2));
    }

    [Fact]
    public async Task A_restore_that_fails_fails_the_sweep()
    {
        var fakes = new Fakes { ExitOf = command => command == "restore" ? 1 : 0 };
        fakes.Labels["a"] = "a";

        var result = await fakes.Sweep().RunAsync(["a"], "grp", Cancellation);

        Assert.True(result.RestoreFailed);
        Assert.True(result.Failed);
    }
}
