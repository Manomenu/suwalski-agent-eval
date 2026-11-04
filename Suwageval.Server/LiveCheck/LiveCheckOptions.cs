using Microsoft.Extensions.Options;

namespace Suwageval.Server.LiveCheck;

internal sealed class LiveCheckOptions
{
    public const string Section = "LiveCheck";

    public string? DiscordWebhookUrl { get; set; }

    internal sealed class LiveCheckOptionsValidator : IValidateOptions<LiveCheckOptions>
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
