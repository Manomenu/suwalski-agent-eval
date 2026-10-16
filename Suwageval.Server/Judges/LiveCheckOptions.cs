using Microsoft.Extensions.Options;

namespace Suwageval.Server.Judges;

/// <summary>The <c>LiveCheck</c> section: read only by the daily check, never by the running server.</summary>
internal sealed class LiveCheckOptions
{
    public const string Section = "LiveCheck";

    /// <summary>A Discord webhook for alerts (a secret, from the environment: <c>LiveCheck__DiscordWebhookUrl</c>); unset means the check only logs and fails.</summary>
    public string? DiscordWebhookUrl { get; set; }

    internal sealed class Validator : IValidateOptions<LiveCheckOptions>
    {
        public ValidateOptionsResult Validate(string? name, LiveCheckOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return
                options.DiscordWebhookUrl is { } url && !(Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == "https")
                ? ValidateOptionsResult.Fail("LiveCheck:DiscordWebhookUrl is an absolute https URL")
                : ValidateOptionsResult.Success;
        }
    }
}
