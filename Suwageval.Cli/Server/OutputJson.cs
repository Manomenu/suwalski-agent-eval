using System.Text.Encodings.Web;
using System.Text.Json;
using Suwageval.AgentSdk;

namespace Suwageval.Cli.Server;

/// <summary>What <c>--json</c> prints: the contract's own JSON, indented, with non-ASCII text left readable.</summary>
internal static class OutputJson
{
    private static readonly JsonSerializerOptions Options = new(ContractJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Of<T>(T value) => JsonSerializer.Serialize(value, Options);
}
