using System.Security.Cryptography;
using System.Text;

namespace SchoolManagement.Setup;

/// <summary>DPAPI LocalMachine — même entropy que EncryptionService (Application).</summary>
internal static class SetupDpapi
{
    private const string EncryptedPrefix = "ENC:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SchoolManagement.ERP.Scolaire.RDC.v1");

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.LocalMachine);
        return EncryptedPrefix + Convert.ToBase64String(protectedBytes);
    }
}
