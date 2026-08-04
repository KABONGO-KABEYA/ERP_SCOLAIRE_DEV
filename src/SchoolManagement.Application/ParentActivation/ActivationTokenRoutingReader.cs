using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace SchoolManagement.Application.ParentActivation;

/// <summary>Extraction non validante de <c>school_id</c> pour routage Bootstrap (I13 : pas de stockage token).</summary>
public static class ActivationTokenRoutingReader
{
    public static Guid TryReadSchoolId(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            throw new InvalidOperationException("Token d'activation manquant.");
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(jwt))
        {
            throw new InvalidOperationException("Token d'activation illisible.");
        }

        var token = handler.ReadJwtToken(jwt);
        if (TryParseSchoolIdFromPayloadValue(token.Payload.TryGetValue("school_id", out var schoolBoxed) ? schoolBoxed : null, out var fromPayload))
        {
            return fromPayload;
        }

        var schoolRaw = token.Claims.FirstOrDefault(c => c.Type is "school_id" or "schoolId")?.Value
                        ?? TryReadSchoolIdFromPayload(token);

        if (!Guid.TryParse(schoolRaw, out var schoolId) || schoolId == Guid.Empty)
        {
            if (TryReadSchoolIdFromPayloadSegment(jwt, out schoolId))
            {
                return schoolId;
            }

            throw new InvalidOperationException(
                "Impossible de résoudre l'école depuis le token (claim school_id manquant).");
        }

        return schoolId;
    }

    public static Guid TryReadTokenId(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        var jti = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value
                  ?? token.Id;
        if (!Guid.TryParse(jti, out var tokenId))
        {
            throw new InvalidOperationException("Claim jti manquant sur le token d'activation.");
        }

        return tokenId;
    }

    private static string? TryReadSchoolIdFromPayload(JwtSecurityToken token)
    {
        return token.Payload.TryGetValue("school_id", out var boxed)
            ? PayloadValueAsString(boxed)
            : null;
    }

    private static bool TryParseSchoolIdFromPayloadValue(object? boxed, out Guid schoolId)
    {
        schoolId = Guid.Empty;
        var raw = PayloadValueAsString(boxed);
        return !string.IsNullOrWhiteSpace(raw)
               && Guid.TryParse(raw, out schoolId)
               && schoolId != Guid.Empty;
    }

    private static string? PayloadValueAsString(object? boxed)
    {
        if (boxed is null)
        {
            return null;
        }

        if (boxed is JsonElement je)
        {
            return je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString();
        }

        return boxed.ToString();
    }

    private static bool TryReadSchoolIdFromPayloadSegment(string jwt, out Guid schoolId)
    {
        schoolId = Guid.Empty;
        var parts = jwt.Split('.');
        if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
        {
            return false;
        }

        try
        {
            var jsonBytes = Base64UrlEncoder.DecodeBytes(parts[1]);
            using var doc = JsonDocument.Parse(jsonBytes);
            if (!doc.RootElement.TryGetProperty("school_id", out var prop))
            {
                return false;
            }

            var raw = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
            return !string.IsNullOrWhiteSpace(raw)
                   && Guid.TryParse(raw, out schoolId)
                   && schoolId != Guid.Empty;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
