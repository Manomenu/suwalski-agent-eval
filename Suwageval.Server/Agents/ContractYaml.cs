using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Agents;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Suwageval.Server.Agents;

internal static partial class ContractYaml
{
    private static readonly JsonSerializerOptions Shape = new(ContractJson.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Of(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return Of(JsonSerializer.SerializeToNode(contract, Shape));
    }

    public static string Of(JsonNode? json)
    {
        var root = Node(json);
        using var writer = new StringWriter { NewLine = "\n" };
        var emitter = new Emitter(
            writer,
            EmitterSettings.Default.WithBestIndent(2).WithBestWidth(int.MaxValue).WithMaxSimpleKeyLength(1024).WithNewLine("\n")
        );
        new YamlStream(new YamlDocument(root)).Save(emitter, assignAnchors: false);
        // Why: the emitter closes a document that ends in an open scalar with '...', which is not part of the compared text
        var yaml = writer.ToString();
        return yaml.EndsWith("\n...\n", StringComparison.Ordinal) ? yaml[..^4] : yaml;
    }

    private static YamlNode Node(JsonNode? json) =>
        json switch
        {
            null => new YamlScalarNode("null"),
            JsonObject obj => Mapping(obj),
            JsonArray array => new YamlSequenceNode(array.Select(Node)),
            _ => Scalar(json),
        };

    private static YamlMappingNode Mapping(JsonObject obj)
    {
        var mapping = new YamlMappingNode();
        foreach (var (key, value) in obj)
        {
            mapping.Add(Text(key), Node(value));
        }
        return mapping;
    }

    private static YamlScalarNode Scalar(JsonNode json)
    {
        if (json.GetValueKind() != JsonValueKind.String)
        {
            return new YamlScalarNode(json.ToJsonString());
        }
        return Text(json.GetValue<string>());
    }

    private static YamlScalarNode Text(string text) =>
        new(text)
        {
            Style =
                text.Contains('\n') ? ScalarStyle.Literal
                : NotAString().IsMatch(text) ? ScalarStyle.DoubleQuoted
                : ScalarStyle.Plain,
        };

    [GeneratedRegex(
        @"^(|~|[nN]ull|NULL|[tT]rue|TRUE|[fF]alse|FALSE|[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?|0x[0-9a-fA-F]+|0o[0-7]+|[-+]?\.(inf|Inf|INF)|\.(nan|NaN|NAN))$"
    )]
    private static partial Regex NotAString();
}
