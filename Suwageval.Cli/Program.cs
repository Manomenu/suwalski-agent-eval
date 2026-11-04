using Suwageval.Cli;

// Why: a second Ctrl+C while the cancel question waits must end the process at once, as Ctrl+C always does
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
