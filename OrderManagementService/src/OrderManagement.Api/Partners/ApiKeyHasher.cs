using System.Security.Cryptography;
using System.Text;

namespace OrderManagement.Api.Partners
{
    // SHA-256, not a slow password hash (bcrypt/PBKDF2) - API keys are
    // high-entropy random strings we generate, not human-chosen
    // passwords, so the slow-hash defense against brute-forcing
    // low-entropy input doesn't apply. A fast cryptographic hash is
    // standard practice for API keys.
    public static class ApiKeyHasher
    {
        public static string Hash(string rawKey)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
            return Convert.ToHexString(hashBytes);
        }

        // Cryptographically random raw key - shown to the admin exactly once, never persisted in raw form
        public static string GenerateRawKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

    }
}
