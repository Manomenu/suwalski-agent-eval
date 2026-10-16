using System.Net.Http.Json;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Validate;

/// <summary>
/// Reads the contract the running agent serves (<c>GET {endpointUrl}/suwageval/contract</c>), so that tool and event
/// names in the files can be checked against what the agent really has.
/// </summary>
internal static class ContractFetcher
{
    /// <summary>How long <c>validate</c> waits for the agent: it is a check at the keyboard, not a run.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>The contract, or in <c>Failure</c> why there is none, as the end of "names not checked: …".</summary>
    public static async Task<(AgentContract? Contract, string? Failure)> FetchAsync(
        HttpClient http,
        Uri endpoint,
        CancellationToken cancellationToken
    )
    {
        var url = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/suwageval/contract");
        var agent = $"agent at {endpoint.AbsoluteUri.TrimEnd('/')}";
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"{agent} has no readable contract ({url} answered {(int)response.StatusCode} {response.ReasonPhrase})");
            }
            var contract = await response.Content.ReadFromJsonAsync<AgentContract>(ContractJson.Options, cancellationToken);
            return contract is null ? (null, $"{agent} served an empty contract") : (contract, null);
        }
        catch (HttpRequestException error)
        {
            return (null, $"{agent} unreachable ({error.Message})");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, $"{agent} unreachable (no answer within {http.Timeout.TotalSeconds:0.##} s)");
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            return (null, $"{agent} served a contract that does not match the format ({error.Message})");
        }
    }
}
