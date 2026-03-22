using System;
using System.Security.Cryptography;
using System.Text;

namespace DhCodetaskExtension.Core.Services
{
    /// <summary>
    /// Computes and verifies SHA-256 checksums for report integrity.
    /// </summary>
    public static class ChecksumHelper
    {
        /// <summary>
        /// Computes SHA-256 hash of the input string (UTF-8 encoded).
        /// Returns lowercase hex string.
        /// </summary>
        public static string Compute(string content)
        {
            if (content == null) return string.Empty;
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
                var sb = new StringBuilder(64);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Verifies that the content matches the expected checksum.
        /// </summary>
        public static bool Verify(string content, string expectedChecksum)
        {
            if (string.IsNullOrEmpty(expectedChecksum)) return false;
            return string.Equals(Compute(content), expectedChecksum,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
