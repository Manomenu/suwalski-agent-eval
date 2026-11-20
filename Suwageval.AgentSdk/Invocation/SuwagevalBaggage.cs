using System.Globalization;
using System.Text;

namespace Suwageval.AgentSdk.Invocation;

public static class SuwagevalBaggage
{
    public const string HeaderName = "baggage";

    public const string RunId = "suwageval.run.id";
    public const string AgentVersionId = "suwageval.agent.version.id";
    public const string ScenarioKey = "suwageval.scenario.key";
    public const string ScenarioTypeKey = "suwageval.scenario.type";
    public const string ScenarioSample = "suwageval.scenario.sample";
    public const string ScenarioTags = "suwageval.scenario.tags";
    public const string RunTags = "suwageval.run.tags";
    public const string RunGroup = "suwageval.run.group";

    public static string Encode(IEnumerable<KeyValuePair<string, string>> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        return string.Join(',', members.Select(member => $"{member.Key}={EncodeValue(member.Value)}"));
    }

    public static IReadOnlyDictionary<string, string> Decode(IEnumerable<string> headerValues)
    {
        ArgumentNullException.ThrowIfNull(headerValues);
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var member in headerValues.SelectMany(value => value.Split(',')))
        {
            var pair = member.Split(';')[0];
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }
            members[pair[..equals].Trim()] = Uri.UnescapeDataString(pair[(equals + 1)..].Trim());
        }
        return members;
    }

    private static string EncodeValue(string value)
    {
        var encoded = new StringBuilder();
        foreach (var octet in Encoding.UTF8.GetBytes(value))
        {
            if (IsBaggageOctet(octet))
            {
                encoded.Append((char)octet);
            }
            else
            {
                encoded.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return encoded.ToString();
    }

    private static bool IsBaggageOctet(byte octet) =>
        octet
            is 0x21
                or (>= 0x23 and <= 0x24)
                or (>= 0x26 and <= 0x2B)
                or (>= 0x2D and <= 0x3A)
                or (>= 0x3C and <= 0x5B)
                or (>= 0x5D and <= 0x7E);
}
