namespace SchoolManagement.Application.Configuration.Encryption;

/// <summary>Chiffrement réversible des secrets de configuration (mot de passe SQL).</summary>
public interface IEncryptionService
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);

    bool IsEncrypted(string? value);
}
