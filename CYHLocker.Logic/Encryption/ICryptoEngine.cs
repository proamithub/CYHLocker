namespace CYHLocker.Logic.Encryption;

public interface ICryptoEngine
{
    /// <summary>
    /// Encrypts plain text using AES-256-GCM with a cryptographically random 12-byte Nonce and 16-byte Tag.
    /// Returns Base64 encoded payload [Nonce(12) + Tag(16) + Ciphertext].
    /// </summary>
    string Encrypt(string plainText, string? key = null);

    /// <summary>
    /// Decrypts AES-256-GCM Base64 payload. Validates authenticity tag.
    /// </summary>
    string Decrypt(string encryptedPayload, string? key = null);
}
