using System.Security.Cryptography;
using System.Text;

namespace SchoolManagement.Application.Configuration.Encryption;

/// <summary>
/// Chiffrement portable (AES) pour Linux/Docker.
/// Clé : variable d'environnement <c>ERP_CONFIG_ENCRYPTION_KEY</c> (recommandé),
/// sinon clé de développement (à ne pas utiliser en production).
/// </summary>
public sealed class AesConfigurationEncryptionService : IEncryptionService
{
    public const string EncryptedPrefix = "AES:";

    private readonly byte[] _key;

    public AesConfigurationEncryptionService()
    {
        var raw = Environment.GetEnvironmentVariable("ERP_CONFIG_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = "SchoolManagement.ERP.Docker.DevKey.ChangeMe";
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw.Trim()));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var payload = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
        return EncryptedPrefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        if (!cipherText.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Le mot de passe SQL doit être chiffré (AES). En Docker, préférez SQL_CONNECTION_STRING.");
        }

        var payload = Convert.FromBase64String(cipherText[EncryptedPrefix.Length..]);
        if (payload.Length < 12 + 16)
        {
            throw new InvalidOperationException("Payload AES invalide.");
        }

        var nonce = payload.AsSpan(0, 12);
        var tag = payload.AsSpan(12, 16);
        var cipher = payload.AsSpan(28);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    public bool IsEncrypted(string? cipherText) =>
        !string.IsNullOrWhiteSpace(cipherText)
        && cipherText.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
}
