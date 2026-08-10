using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SchoolManagement.Application.SchoolEstablishment;

/// <summary>
/// Crypto credential établissement — aligné Bootstrap Phase 3 :
/// HMAC key = SHA-256(UTF-8(SecretHash)).
/// </summary>
public static class SchoolEstablishmentCrypto
{
    public static string HashSecret(ReadOnlySpan<byte> rawSecret) =>
        Convert.ToHexString(SHA256.HashData(rawSecret)).ToLowerInvariant();

    public static byte[] GenerateRawSecret(int byteLength = 32)
    {
        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    /// <summary>Produit (SecretHash, discarde le brut). Ne jamais logger le brut.</summary>
    public static string CreateSecretHash()
    {
        var raw = GenerateRawSecret();
        try
        {
            return HashSecret(raw);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }
    }

    public static SymmetricSecurityKey CreateSigningKey(string secretHash)
    {
        var material = Encoding.UTF8.GetBytes(secretHash.Trim());
        var derived = SHA256.HashData(material);
        return new SymmetricSecurityKey(derived);
    }

    public static string CreateSignedJwt(
        Guid schoolId,
        Guid credentialId,
        int credentialVersion,
        string secretHash,
        DateTime? expiresUtc = null)
    {
        var creds = new SigningCredentials(CreateSigningKey(secretHash), SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(SchoolEstablishmentTokenConstants.TokenTypeClaim, SchoolEstablishmentTokenConstants.TokenTypeValue),
            new(SchoolEstablishmentTokenConstants.SchoolIdClaim, schoolId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, credentialId.ToString("D")),
            new(SchoolEstablishmentTokenConstants.VersionClaim, credentialVersion.ToString()),
        };

        var jwt = new JwtSecurityToken(
            issuer: SchoolEstablishmentTokenConstants.SchoolIssuer(schoolId),
            audience: SchoolEstablishmentTokenConstants.Audience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: expiresUtc ?? now.AddDays(3650),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public static string BuildDeepLink(string jwt) =>
        $"{SchoolEstablishmentTokenConstants.DeepLinkScheme}://{SchoolEstablishmentTokenConstants.DeepLinkPath}?token={Uri.EscapeDataString(jwt)}";
}
