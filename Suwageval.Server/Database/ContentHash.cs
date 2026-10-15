using System.Security.Cryptography;
using System.Text;

namespace Suwageval.Server.Database;

/// <summary>The fingerprint of agent-defined content: what tells "the same again" from "changed".</summary>
internal static class ContentHash
{
    /// <summary>SHA-256 of the text as sent, in lowercase hex.</summary>
    public static string Of(string contentJson) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contentJson)));
}
