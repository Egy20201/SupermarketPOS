using System;
using System.Security.Cryptography;

namespace SupermarketPOS.Core.Security
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;
        private const string Prefix = "PBKDF2";

        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty.", nameof(password));

            using (var rng = RandomNumberGenerator.Create())
            {
                var salt = new byte[SaltSize];
                rng.GetBytes(salt);
                var hash = ComputeHash(password, salt, Iterations, HashSize);
                return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
            }
        }

        public static bool VerifyPassword(string password, string storedValue)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedValue))
                return false;

            if (!IsHashed(storedValue))
                return false;

            var parts = storedValue.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
                return false;

            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = ComputeHash(password, salt, iterations, expectedHash.Length);

            return FixedTimeEquals(actualHash, expectedHash);
        }

        public static bool IsHashed(string storedValue)
        {
            return !string.IsNullOrWhiteSpace(storedValue) &&
                   storedValue.StartsWith(Prefix + "$", StringComparison.Ordinal);
        }

        private static byte[] ComputeHash(string password, byte[] salt, int iterations, int outputBytes)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(outputBytes);
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }
    }
}
