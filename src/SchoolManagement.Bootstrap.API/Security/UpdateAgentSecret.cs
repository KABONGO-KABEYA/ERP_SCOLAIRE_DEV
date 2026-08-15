using System.Security.Cryptography;
using System.Text;

namespace SchoolManagement.Bootstrap.API.Security;

/// <summary>Génération et hachage du ClientSecret agent. Ne jamais journaliser le secret.</summary>
public static class UpdateAgentSecret
{
    public const int RawSecretByteLength = 32;

    public static (string ClientSecret, string SecretHash) Generate()
    {
        var raw = new byte[RawSecretByteLength];
        RandomNumberGenerator.Fill(raw);
        try
        {
            var clientSecret = Base64UrlEncode(raw);
            return (clientSecret, HashRaw(raw));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }
    }

    public static bool Matches(string providedSecret, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(providedSecret) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        if (!TryBase64UrlDecode(providedSecret.Trim(), out var raw) || raw.Length == 0)
        {
            return false;
        }

        try
        {
            var computed = HashRaw(raw);
            var left = Encoding.UTF8.GetBytes(computed);
            var right = Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant());
            if (left.Length != right.Length)
            {
                CryptographicOperations.FixedTimeEquals(left, left);
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(left, right);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }
    }

    public static bool IsSha256Hex(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length == 64
        && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static string HashRaw(ReadOnlySpan<byte> raw) =>
        Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryBase64UrlDecode(string value, out byte[] bytes)
    {
        bytes = [];
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            case 1:
                return false;
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
