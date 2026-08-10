using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SchoolManagement.Bootstrap.API.Establishment;

/// <summary>
/// Lecture + validation HMAC du JWT établissement.
/// Clé HMAC = SHA-256(UTF-8(<c>SecretHash</c>)) — matériel partagé école↔Bootstrap, ≥256 bits.
/// </summary>
public static class EstablishmentJwtValidator
{
    public sealed record ParsedEstablishmentToken(
        Guid SchoolId,
        Guid CredentialId,
        int CredentialVersion,
        string TokenType);

    /// <summary>Lecture non validante des claims (avant lookup registre / credential).</summary>
    public static ParsedEstablishmentToken ReadClaims(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "Token établissement manquant.");
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            throw new EstablishmentException(
                StatusCodes.Status401Unauthorized,
                "Token établissement invalide.");
        }

        JwtSecurityToken jwt;
        try
        {
            jwt = handler.ReadJwtToken(token);
        }
        catch (Exception)
        {
            throw new EstablishmentException(
                StatusCodes.Status401Unauthorized,
                "Token établissement invalide.");
        }

        var tokenType = GetClaim(jwt, EstablishmentTokenConstants.TokenTypeClaim)
                        ?? GetClaim(jwt, "typ");

        if (!string.Equals(tokenType, EstablishmentTokenConstants.TokenTypeValue, StringComparison.Ordinal))
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "Token non valide pour l'établissement (type incorrect).");
        }

        if (!TryParseGuid(GetClaim(jwt, EstablishmentTokenConstants.SchoolIdClaim)
                          ?? GetClaim(jwt, "schoolId"), out var schoolId))
        {
            throw new EstablishmentException(
                StatusCodes.Status401Unauthorized,
                "Token établissement invalide.");
        }

        var jti = GetClaim(jwt, JwtRegisteredClaimNames.Jti) ?? jwt.Id;
        if (!TryParseGuid(jti, out var credentialId))
        {
            throw new EstablishmentException(
                StatusCodes.Status401Unauthorized,
                "Token établissement invalide.");
        }

        if (!TryParseInt(GetClaim(jwt, EstablishmentTokenConstants.VersionClaim), out var version) || version < 1)
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "Version de credential invalide.");
        }

        return new ParsedEstablishmentToken(
            schoolId,
            credentialId,
            version,
            EstablishmentTokenConstants.TokenTypeValue);
    }

    public static void ValidateSignature(string token, string secretHash, Guid schoolId)
    {
        if (string.IsNullOrWhiteSpace(secretHash))
        {
            throw new EstablishmentException(
                StatusCodes.Status401Unauthorized,
                "Token établissement invalide.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSigningKey(secretHash),
            ValidateIssuer = true,
            ValidIssuers =
            [
                EstablishmentTokenConstants.SchoolIssuer(schoolId),
                EstablishmentTokenConstants.BootstrapIssuer,
            ],
            ValidateAudience = true,
            ValidAudience = EstablishmentTokenConstants.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            throw new EstablishmentException(
                StatusCodes.Status401Unauthorized,
                "Token établissement invalide.");
        }
        catch (ArgumentException)
        {
            throw new EstablishmentException(
                StatusCodes.Status401Unauthorized,
                "Token établissement invalide.");
        }
    }

    /// <summary>Helper tests / Phase 4 — émet un JWT établissement HMAC.</summary>
    public static string CreateSignedToken(
        Guid schoolId,
        Guid credentialId,
        int credentialVersion,
        string secretHash,
        DateTime? expiresUtc = null,
        string? tokenType = null,
        string? issuer = null)
    {
        var creds = new SigningCredentials(CreateSigningKey(secretHash), SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(EstablishmentTokenConstants.TokenTypeClaim, tokenType ?? EstablishmentTokenConstants.TokenTypeValue),
            new(EstablishmentTokenConstants.SchoolIdClaim, schoolId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, credentialId.ToString("D")),
            new(EstablishmentTokenConstants.VersionClaim, credentialVersion.ToString()),
        };

        var jwt = new JwtSecurityToken(
            issuer: issuer ?? EstablishmentTokenConstants.SchoolIssuer(schoolId),
            audience: EstablishmentTokenConstants.Audience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: expiresUtc ?? now.AddDays(3650),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static SymmetricSecurityKey CreateSigningKey(string secretHash)
    {
        var material = Encoding.UTF8.GetBytes(secretHash.Trim());
        var derived = SHA256.HashData(material);
        return new SymmetricSecurityKey(derived);
    }

    private static string? GetClaim(JwtSecurityToken jwt, string type) =>
        jwt.Claims.FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;

    private static bool TryParseGuid(string? raw, out Guid value) =>
        Guid.TryParse(raw, out value) && value != Guid.Empty;

    private static bool TryParseInt(string? raw, out int value) =>
        int.TryParse(raw, out value);
}
