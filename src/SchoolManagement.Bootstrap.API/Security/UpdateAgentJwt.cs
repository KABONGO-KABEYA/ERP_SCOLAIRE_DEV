using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.Bootstrap.API.Persistence.Entities;

namespace SchoolManagement.Bootstrap.API.Security;

public sealed record UpdateAgentJwtClaims(
    Guid ClientId,
    Guid JwtId,
    Guid SchoolId,
    Guid? ServerInstanceId);

/// <summary>
/// JWT agent : HMAC dédié (<c>Bootstrap:AgentJwtSigningKey</c>).
/// <c>jti</c> = GUID unique par émission — jamais l'id du credential.
/// </summary>
public static class UpdateAgentJwt
{
    public static bool TryValidateSigningKey(string? key, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            error = "Signature JWT agent non configurée (Bootstrap:AgentJwtSigningKey).";
            return false;
        }

        var utf8Bytes = Encoding.UTF8.GetByteCount(key.Trim());
        if (utf8Bytes < UpdateAgentTokenConstants.MinSigningKeyUtf8Bytes)
        {
            error = "Bootstrap:AgentJwtSigningKey invalide (HMAC trop courte, minimum 32 octets).";
            return false;
        }

        return true;
    }

    public static string Create(
        string signingKey,
        Guid clientId,
        Guid schoolId,
        Guid? serverInstanceId,
        DateTime expiresUtc,
        string? audience = null,
        string? tokenType = null,
        Guid? jwtId = null)
    {
        if (!TryValidateSigningKey(signingKey, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var jti = jwtId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;
        var notBefore = expiresUtc <= now ? expiresUtc.AddMinutes(-1) : now.AddMinutes(-1);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, jti.ToString("D")),
            new(UpdateAgentTokenConstants.TokenTypeClaim, tokenType ?? UpdateAgentTokenConstants.TokenTypeValue),
            new(UpdateAgentTokenConstants.SchoolIdClaim, schoolId.ToString("D")),
        };

        if (serverInstanceId is { } instanceId && instanceId != Guid.Empty)
        {
            claims.Add(new Claim(UpdateAgentTokenConstants.ServerInstanceIdClaim, instanceId.ToString("D")));
        }

        var jwt = new JwtSecurityToken(
            issuer: UpdateAgentTokenConstants.Issuer,
            audience: audience ?? UpdateAgentTokenConstants.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expiresUtc,
            signingCredentials: new SigningCredentials(CreateSigningKey(signingKey), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler { MapInboundClaims = false }.WriteToken(jwt);
    }

    public static UpdateAgentJwtClaims Validate(string token, string signingKey)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SecurityTokenException("Jeton agent manquant.");
        }

        if (!TryValidateSigningKey(signingKey, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        JwtSecurityToken jwt;
        try
        {
            jwt = handler.ReadJwtToken(token);
        }
        catch (ArgumentException ex)
        {
            throw new SecurityTokenException("Jeton agent invalide.", ex);
        }
        catch (SecurityTokenException)
        {
            throw;
        }

        var tokenType = GetClaim(jwt, UpdateAgentTokenConstants.TokenTypeClaim);
        if (!string.Equals(tokenType, UpdateAgentTokenConstants.TokenTypeValue, StringComparison.Ordinal))
        {
            throw new SecurityTokenException("Type de jeton agent invalide.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSigningKey(signingKey),
            ValidateIssuer = true,
            ValidIssuer = UpdateAgentTokenConstants.Issuer,
            ValidateAudience = true,
            ValidAudience = UpdateAgentTokenConstants.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        handler.ValidateToken(token, parameters, out _);

        if (!TryParseGuid(GetClaim(jwt, JwtRegisteredClaimNames.Sub) ?? jwt.Subject, out var clientId)
            || clientId == Guid.Empty)
        {
            throw new SecurityTokenException("Jeton agent invalide.");
        }

        var jtiRaw = GetClaim(jwt, JwtRegisteredClaimNames.Jti) ?? jwt.Id;
        if (!TryParseGuid(jtiRaw, out var jwtId) || jwtId == Guid.Empty)
        {
            throw new SecurityTokenException("Jeton agent invalide.");
        }

        if (!TryParseGuid(GetClaim(jwt, UpdateAgentTokenConstants.SchoolIdClaim), out var schoolId)
            || schoolId == Guid.Empty)
        {
            throw new SecurityTokenException("Jeton agent invalide.");
        }

        Guid? serverInstanceId = null;
        var instanceRaw = GetClaim(jwt, UpdateAgentTokenConstants.ServerInstanceIdClaim);
        if (TryParseGuid(instanceRaw, out var parsedInstance) && parsedInstance != Guid.Empty)
        {
            serverInstanceId = parsedInstance;
        }

        return new UpdateAgentJwtClaims(clientId, jwtId, schoolId, serverInstanceId);
    }

    private static SymmetricSecurityKey CreateSigningKey(string key) =>
        new(Encoding.UTF8.GetBytes(key.Trim()));

    private static string? GetClaim(JwtSecurityToken jwt, string type) =>
        jwt.Claims.FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;

    private static bool TryParseGuid(string? raw, out Guid value) =>
        Guid.TryParse(raw, out value) && value != Guid.Empty;
}
