using System.Security.Cryptography;
using System.Text;

namespace Utilities
{
    public static class Url64Helper
    {
        public static string Encode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentNullException(nameof(input), "Input cannot be empty.");

            if (!Uri.IsWellFormedUriString(input, UriKind.Absolute))
                throw new ArgumentException("Invalid URL format.", nameof(input));

            var sha256 = SHA256.Create();
            var hashByte = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            var shortCode = Convert.ToBase64String(hashByte).Replace("/", "").Replace("+", "").Substring(0, 8);
            return shortCode;
        }
    }
}
