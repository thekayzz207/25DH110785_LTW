using System;
using System.Globalization;
using System.Security.Cryptography;

namespace LTW_25DH110785.Services
{
    public static class PasswordService
    {
        private const int Iterations = 100000;
        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException("password");
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                return "PBKDF2$" + Iterations.ToString(CultureInfo.InvariantCulture) + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(kdf.GetBytes(32));
        }
        public static bool Verify(string password, string encoded)
        {
            if (password == null || string.IsNullOrWhiteSpace(encoded)) return false;
            try
            {
                var parts = encoded.Split('$'); int iterations;
                if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], out iterations) || iterations < 100000 || iterations > 1000000) return false;
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                if (salt.Length != 16 || expected.Length != 32) return false;
                using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                {
                    var actual = kdf.GetBytes(32); int difference = 0;
                    for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
                    return difference == 0;
                }
            }
            catch (FormatException) { return false; }
            catch (ArgumentException) { return false; }
        }
    }
}
