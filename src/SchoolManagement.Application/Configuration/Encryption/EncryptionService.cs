using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace SchoolManagement.Application.Configuration.Encryption;

/// <summary>
/// Chiffre les mots de passe via DPAPI (LocalMachine) pour éviter le stockage en clair.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EncryptionService : IEncryptionService
{
    public const string EncryptedPrefix = "ENC:";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SchoolManagement.ERP.Scolaire.RDC.v1");

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.LocalMachine);
        return EncryptedPrefix + Convert.ToBase64String(protectedBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        if (!IsEncrypted(cipherText))
        {
            throw new InvalidOperationException(
                "Le mot de passe SQL doit être chiffré. Ouvrez la configuration du serveur et enregistrez à nouveau.");
        }

        var payload = cipherText[EncryptedPrefix.Length..];
        var protectedBytes = Convert.FromBase64String(payload);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public bool IsEncrypted(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith(EncryptedPrefix, StringComparison.OrdinalIgnoreCase);
}
