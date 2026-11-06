using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace CYHLocker.Logic.Encryption;

public class AesGcmCryptoEngine : ICryptoEngine
{
    private readonly byte[] _defaultKey;
    private const int NonceSize = 12; // 96-bit recommended for GCM
    private const int TagSize = 16;   // 128-bit authentication tag

    public AesGcmCryptoEngine(IConfiguration config)
    {
        // 256-bit (32 bytes) master vault encryption key
        var rawKey = config["Vault:MasterKey"] 
            ?? config["Jwt:Key"] 
            ?? "CYHLocker_SuperVaultMasterKey_AES256_2026_AmitLab!";

        // Ensure key is exactly 32 bytes using SHA-256 derivation
        using var sha = SHA256.Create();
        _defaultKey = sha.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
    }

    public string Encrypt(string plainText, string? customKey = null)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var key = customKey != null ? DeriveKey(customKey) : _defaultKey;
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[TagSize];
        var cipherBytes = new byte[plainBytes.Length];

        using (var aesGcm = new AesGcm(key, TagSize))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        // Combine: Nonce + Tag + Ciphertext
        var combined = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string encryptedPayload, string? customKey = null)
    {
        if (string.IsNullOrEmpty(encryptedPayload)) return string.Empty;

        var key = customKey != null ? DeriveKey(customKey) : _defaultKey;
        var combined = Convert.FromBase64String(encryptedPayload);

        if (combined.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext payload size.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherLength = combined.Length - NonceSize - TagSize;
        var cipherBytes = new byte[cipherLength];

        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(combined, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(combined, NonceSize + TagSize, cipherBytes, 0, cipherLength);

        var plainBytes = new byte[cipherLength];

        using (var aesGcm = new AesGcm(key, TagSize))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] DeriveKey(string raw)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
    }
}
