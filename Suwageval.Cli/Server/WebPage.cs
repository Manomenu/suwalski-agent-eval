using System.ComponentModel;
using System.Diagnostics;

namespace Suwageval.Cli.Server;

/// <summary>What <c>results</c> and <c>compare</c> share: a page of the web app is printed — for a terminal with no browser — and opened.</summary>
internal static class WebPage
{
    /// <param name="page">The page to print and open.</param>
    /// <param name="output">Where the link is printed.</param>
    /// <param name="error">Where a note goes when no browser could be opened.</param>
    /// <param name="openBrowser">Opens a page; none means the system's default browser (the tests record the page instead).</param>
    public static async Task ShowAsync(Uri page, TextWriter output, TextWriter error, Action<Uri>? openBrowser)
    {
        await output.WriteLineAsync(page.ToString());
        try
        {
            (openBrowser ?? OpenInDefaultBrowser)(page);
        }
        catch (Win32Exception)
        {
            // No browser here (an SSH session, CI): the link above is the way in.
            await error.WriteLineAsync("No browser could be opened: open the link above.");
        }
    }

    // With the shell, .NET hands a URL to the system's opener (xdg-open, open, start).
    private static void OpenInDefaultBrowser(Uri page) =>
        Process.Start(new ProcessStartInfo(page.ToString()) { UseShellExecute = true })?.Dispose();
}
