using System.Security.Cryptography;
using System.Text;

namespace Suwageval.Server.Database;

internal static class ContentHash
{
    public static string Of(string contentJson) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contentJson)));
}
