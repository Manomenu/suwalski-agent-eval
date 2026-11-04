using System.ComponentModel;
using System.Diagnostics;

namespace Suwageval.Cli.Server;

internal static class WebPage
{
    public static async Task ShowAsync(Uri page, TextWriter output, TextWriter error, Action<Uri>? openBrowser)
    {
        await output.WriteLineAsync(page.ToString());
        try
        {
            (openBrowser ?? OpenInDefaultBrowser)(page);
        }
        catch (Win32Exception)
        {
            await error.WriteLineAsync("No browser could be opened: open the link above.");
        }
    }

    private static void OpenInDefaultBrowser(Uri page) =>
        Process.Start(new ProcessStartInfo(page.ToString()) { UseShellExecute = true })?.Dispose();
}
