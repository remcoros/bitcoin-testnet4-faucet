using System.Security.Cryptography;
using System.Text;

namespace Faucet.Utilities;

public static class FaucetUserHashGenerator
{
    public static string GenerateHash(string salt, string provider, string userId)
    {
        // create a cryptographically unique irreversible hash for the user and provider
        var combined = $"{provider}:{userId}:{salt}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hashBytes);
    }
}