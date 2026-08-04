using System.Text;
using SchoolManagement.Application.Configuration.Encryption;

namespace SchoolManagement.UnitTests.Foundations;

/// <summary>Chiffrement déterministe pour tests ServerIdentity (indépendant de DPAPI/AES).</summary>
internal sealed class TestRoundTripEncryption : IEncryptionService
{
    private const string Prefix = "TEST:";

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Payload test invalide.");
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText[Prefix.Length..]));
    }

    public bool IsEncrypted(string? cipherText) =>
        !string.IsNullOrWhiteSpace(cipherText)
        && cipherText.StartsWith(Prefix, StringComparison.Ordinal);
}
