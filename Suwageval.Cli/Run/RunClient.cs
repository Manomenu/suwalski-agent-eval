using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Run;

/// <summary>The server's run endpoints, spoken with the contract records. Anything that goes wrong is a <see cref="RunClientException"/>.</summary>
internal sealed class RunClient(HttpClient http)
{
    public Task<RunCreated> CreateAsync(RunRequest request, CancellationToken cancellationToken) =>
        SendAsync<RunCreated>(
            () => http.PostAsJsonAsync(new Uri("runs", UriKind.Relative), request, ContractJson.Options, cancellationToken),
            cancellationToken
        );

    public Task<RunSummary> GetAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunSummary>(() => http.GetAsync(new Uri($"runs/{runId}", UriKind.Relative), cancellationToken), cancellationToken);

    public Task<RunResults> ResultsAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunResults>(
            () => http.GetAsync(new Uri($"runs/{runId}/results", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    private static async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            if (!response.IsSuccessStatusCode)
            {
                throw new RunClientException(Describe(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken)));
            }
            return await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, cancellationToken)
                ?? throw new RunClientException("The server's answer was empty.");
        }
        catch (HttpRequestException error)
        {
            throw new RunClientException($"The server could not be reached: {error.Message}");
        }
        catch (JsonException error)
        {
            throw new RunClientException($"The server's answer could not be read: {error.Message}");
        }
    }

    /// <summary>The Problem Details of the server in words: its detail, or every field error of a validation problem.</summary>
    private static string Describe(System.Net.HttpStatusCode status, string body)
    {
        var text = new StringBuilder().Append(CultureInfo.InvariantCulture, $"The server answered {(int)status} {status}");
        try
        {
            using var problem = JsonDocument.Parse(body);
            var root = problem.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        text.Append(
                            CultureInfo.InvariantCulture,
                            $": {field.Name}: {string.Join(" ", field.Value.EnumerateArray().Select(message => message.GetString()))}"
                        );
                    }
                }
                else if (root.TryGetProperty("mismatches", out var mismatches) && mismatches.ValueKind == JsonValueKind.Array)
                {
                    text.Append(": the agent's version does not fit its scenarios:");
                    foreach (var mismatch in mismatches.EnumerateArray())
                    {
                        text.Append("\n  - ").Append(mismatch.GetString());
                    }
                }
                else if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                {
                    text.Append(": ").Append(detail.GetString());
                }
            }
        }
        catch (JsonException)
        {
            // Not Problem Details (a proxy's page, say): the status alone has to do.
        }
        return text.ToString();
    }
}
