using System.Security.Cryptography;
using System.Text;

namespace CarServ.MVC.Helpers
{
    public static class PasswordHelper
    {
        /// <summary>
        /// Hash password với salt ngẫu nhiên
        /// </summary>
        public static (string Hash, string Salt) HashPassword(string password)
        {
            // Tạo salt ngẫu nhiên
            byte[] saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            string salt = Convert.ToBase64String(saltBytes);

            // Hash password với salt
            string hash = HashPasswordWithSalt(password, salt);

            return (hash, salt);
        }

        /// <summary>
        /// Hash password với salt đã có
        /// </summary>
        public static string HashPasswordWithSalt(string password, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            // Kết hợp password và salt
            byte[] combinedBytes = new byte[passwordBytes.Length + saltBytes.Length];
            Buffer.BlockCopy(passwordBytes, 0, combinedBytes, 0, passwordBytes.Length);
            Buffer.BlockCopy(saltBytes, 0, combinedBytes, passwordBytes.Length, saltBytes.Length);

            // Hash với SHA256
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(combinedBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Verify password
        /// </summary>
        public static bool VerifyPassword(string password, string hash, string salt)
        {
            string computedHash = HashPasswordWithSalt(password, salt);
            return computedHash == hash;
        }
    }
}


