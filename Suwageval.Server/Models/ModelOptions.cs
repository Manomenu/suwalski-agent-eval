namespace Suwageval.Server.Models;

/// <summary>One model Suwageval itself calls: <c>Models:&lt;name&gt;</c> in the configuration.</summary>
internal sealed class ModelOptions
{
    /// <summary>The OpenAI-compatible server; its endpoints are directly under it (<c>{BaseUrl}/chat/completions</c>).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The model's name on that server.</summary>
    public string Model { get; set; } = "";

    /// <summary>The server's API key; null or empty for a server that asks for none.</summary>
    public string? ApiKey { get; set; }

    /// <summary>How many calls to this model are in flight at once, whoever asks (TODO.md, "Parallel runs").</summary>
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>How long one call may take once it has a slot.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>What is wrong with this model's settings, or null.</summary>
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
