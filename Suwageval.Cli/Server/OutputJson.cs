using System.Text.Encodings.Web;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Server;

/// <summary>What <c>--json</c> prints: the contract's own JSON, indented, in ASCII (see <see cref="AsciiJson"/>).</summary>
internal static class OutputJson
{
    private static readonly JsonSerializerOptions Options = new(ContractJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Of<T>(T value) => AsciiJson.Escape(JsonSerializer.Serialize(value, Options));
}
