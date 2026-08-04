using System.Security.Cryptography;

namespace SchoolManagement.Infrastructure.ServerIdentity;

internal static class ServerIdentityKeyMaterial
{
    public static (byte[] PublicKeySpki, byte[] PrivateKeyPkcs8, string Fingerprint) GenerateRsa2048()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        var privateKey = rsa.ExportPkcs8PrivateKey();
        var fingerprint = ComputeFingerprint(publicKey);
        return (publicKey, privateKey, fingerprint);
    }

    public static string ComputeFingerprint(ReadOnlySpan<byte> publicKeySpki)
    {
        var hash = SHA256.HashData(publicKeySpki);
        return "sha256:" + Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string EncodePrivateKeyForStorage(byte[] privateKeyPkcs8) =>
        Convert.ToBase64String(privateKeyPkcs8);

    public static byte[] DecodePrivateKeyFromStorage(string encoded) =>
        Convert.FromBase64String(encoded);
}
