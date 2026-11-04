using System.Text.Encodings.Web;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Cli.Files;

namespace Suwageval.Cli.Output;

internal static class OutputJson
{
    private static readonly JsonSerializerOptions Options = new(ContractJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Of<T>(T value) => AsciiJson.Escape(JsonSerializer.Serialize(value, Options));
}
