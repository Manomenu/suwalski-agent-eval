using Suwageval.Cli;

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
