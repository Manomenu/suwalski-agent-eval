namespace Suwageval.Server.LanguageModel;

internal sealed class ModelOptions
{
    public string BaseUrl { get; set; } = "";

    public string Model { get; set; } = "";

    public string? ApiKey { get; set; }

    public int MaxConcurrency { get; set; } = 8;

    public int TimeoutSeconds { get; set; } = 120;

    public string? Problem(string name)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            return $"Models:{name}:BaseUrl is an absolute http or https URL";
        }
        if (string.IsNullOrWhiteSpace(Model))
        {
            return $"Models:{name}:Model is required";
        }
        if (MaxConcurrency <= 0)
        {
            return $"Models:{name}:MaxConcurrency must be above 0";
        }
        return TimeoutSeconds <= 0 ? $"Models:{name}:TimeoutSeconds must be above 0" : null;
    }
}
