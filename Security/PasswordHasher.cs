using System;
using System.Security.Cryptography;

namespace mi_ferreteria.Security
{
    public static class PasswordHasher
    {
        public static (byte[] Hash, byte[] Salt) HashPassword(string password, int iterations = 100_000)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password vacío", nameof(password));
            var salt = RandomNumberGenerator.GetBytes(16); // 128-bit salt
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32); // 256-bit hash
            return (hash, salt);
        }

        public static bool Verify(string password, byte[] salt, byte[] expectedHash, int iterations = 100_000)
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(hash, expectedHash);
        }
    }
}

