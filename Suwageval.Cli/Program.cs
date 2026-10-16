using Suwageval.Cli;

// The entry point only hands over the process's streams and Ctrl+C; what the CLI is lives in CliApp.cs.
// The first Ctrl+C cancels what the command is doing (run --wait then asks whether to cancel the run too); a second one, while
// the question waits, ends the process at once, as Ctrl+C always does.
using var interrupt = new CancellationTokenSource();
Console.CancelKeyPress += (_, press) =>
{
    if (!interrupt.IsCancellationRequested)
    {
        press.Cancel = true;
        interrupt.Cancel();
    }
};
return CliApp.Run(args, Console.In, Console.Out, Console.Error, interactive: !Console.IsInputRedirected, interrupt: interrupt.Token);
