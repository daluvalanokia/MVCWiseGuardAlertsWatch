using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MVCWiseGuardAlertsWatch.Services
{
    /// <summary>AES-GCM encryption for provider API keys (stored never in plaintext).</summary>
    public class CryptoService
    {
        private readonly byte[] _key;

        public CryptoService(IConfiguration config)
        {
            string keyPath = config["Crypto:KeyFile"] ?? "secret.key";
            if (!File.Exists(keyPath))
                File.WriteAllBytes(keyPath, RandomNumberGenerator.GetBytes(32));
            _key = File.ReadAllBytes(keyPath);
        }

        public string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var nonce = RandomNumberGenerator.GetBytes(12);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[16];
            using var aes = new AesGcm(_key, 16);
            aes.Encrypt(nonce, Encoding.UTF8.GetBytes(plain), cipher, tag);
            return Convert.ToBase64String(nonce.Concat(cipher).Concat(tag).ToArray());
        }

        public string Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                var raw = Convert.FromBase64String(cipherText);
                byte[] nonce = raw[..12], cipher = raw[12..^16], tag = raw[^16..];
                byte[] plain = new byte[cipher.Length];
                using var aes = new AesGcm(_key, 16);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return "";
            }
        }
    }
}
