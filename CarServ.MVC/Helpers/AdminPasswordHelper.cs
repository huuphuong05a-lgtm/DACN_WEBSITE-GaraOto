using System.Security.Cryptography;
using System.Text;

namespace CarServ.MVC.Helpers
{
    public static class AdminPasswordHelper
    {
        /// <summary>
        /// Hash password bằng SHA256
        /// </summary>
        public static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Verify password
        /// </summary>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            string hashInputPassword = HashPassword(password);
            return hashInputPassword == hashedPassword;
        }
    }
}


