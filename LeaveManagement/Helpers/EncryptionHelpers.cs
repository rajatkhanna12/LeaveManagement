using System.Security.Cryptography;
using System.Text;

namespace LeaveManagement.Helpers
{
    public static class EncryptionHelpers
    {
        public static string EncryptDecimal(decimal value)
        {
            var plainText = value.ToString();
            using var aes = Aes.Create();
            aes.Key = GetKey();
            aes.IV = new byte[16];

            var encryptor = aes.CreateEncryptor();
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
            return Convert.ToBase64String(encrypted);
        }

        public static decimal DecryptDecimal(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return 0;
            using var aes = Aes.Create();
            aes.Key = GetKey();
            aes.IV = new byte[16];

            var decryptor = aes.CreateDecryptor();
            var bytes = Convert.FromBase64String(encrypted);
            var decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);
            var str = Encoding.UTF8.GetString(decrypted);
            return decimal.TryParse(str, out var result) ? result : 0;
        }

        private static byte[] GetKey()
        {
            var key = "ThisIsA32ByteKeyForDemoPurposesOnly!";
            return Encoding.UTF8.GetBytes(key[..32]);
        }
    }
}
