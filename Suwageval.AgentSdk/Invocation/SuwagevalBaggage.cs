using System.Globalization;
using System.Text;

namespace Suwageval.AgentSdk.Invocation;

/// <summary>
/// The W3C <c>baggage</c> header suwageval sends with every turn (<c>key=value,key=value</c>, values percent-encoded as UTF-8), and
/// the keys in it. Written by the server and read by <see cref="TurnCorrelation"/>, so both ends share one encoding.
/// </summary>
public static class SuwagevalBaggage
{
    public const string HeaderName = "baggage";

    public const string RunId = "suwageval.run.id";
    public const string ScenarioKey = "suwageval.scenario.key";
    public const string ScenarioTypeKey = "suwageval.scenario.type";
    public const string Sample = "suwageval.sample";
    public const string ScenarioTags = "suwageval.scenario.tags";
    public const string RunTags = "suwageval.run.tags";
    public const string RunGroup = "suwageval.run.group";

    /// <summary>The header's value for these members, in order. A value is percent-encoded unless the W3C grammar allows it as it is.</summary>
    public static string Encode(IEnumerable<KeyValuePair<string, string>> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        return string.Join(',', members.Select(member => $"{member.Key}={EncodeValue(member.Value)}"));
    }

    /// <summary>
    /// The members of one or more <c>baggage</c> header values. A member without <c>=</c> is skipped, properties (<c>;…</c>) are dropped;
    /// when a key repeats the last one wins.
    /// </summary>
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

    // baggage-octet = %x21 / %x23-2B / %x2D-3A / %x3C-5B / %x5D-7E, minus % itself, which starts an escape
    private static bool IsBaggageOctet(byte octet) =>
        octet
            is 0x21
                or (>= 0x23 and <= 0x24)
                or (>= 0x26 and <= 0x2B)
                or (>= 0x2D and <= 0x3A)
                or (>= 0x3C and <= 0x5B)
                or (>= 0x5D and <= 0x7E);
}
