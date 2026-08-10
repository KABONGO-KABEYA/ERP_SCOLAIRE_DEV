using System.IdentityModel.Tokens.Jwt;

namespace SchoolManagement.Application.ParentActivation;

/// <summary>
/// Garde-fou token_type : ParentActivation ≠ QR établissement (Phase 7).
/// </summary>
public static class ParentActivationTokenTypeGuard
{
    public const string SchoolEstablishmentType = "school_establishment";

    public const string RejectedEstablishmentMessage =
        "Token établissement non accepté sur le flux parent (ParentActivation uniquement).";

    public const string InvalidTypeMessage = "Type de token invalide.";

    /// <summary>Lit <c>token_type</c> puis <c>typ</c> (compat JWT parent historique).</summary>
    public static string? ReadTokenType(JwtSecurityToken jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        return jwt.Claims.FirstOrDefault(c =>
                       string.Equals(c.Type, "token_type", StringComparison.OrdinalIgnoreCase))
                   ?.Value
               ?? jwt.Claims.FirstOrDefault(c =>
                       string.Equals(c.Type, ActivationTokenConstants.TokenTypeClaim, StringComparison.OrdinalIgnoreCase))
                   ?.Value;
    }

    /// <summary>Lecture non validante (avant signature) pour rejet précoce.</summary>
    public static string? TryPeekTokenType(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(jwt))
        {
            return null;
        }

        try
        {
            return ReadTokenType(handler.ReadJwtToken(jwt));
        }
        catch
        {
            return null;
        }
    }

    public static bool IsSchoolEstablishmentToken(string? jwt)
    {
        var type = TryPeekTokenType(jwt);
        return string.Equals(type, SchoolEstablishmentType, StringComparison.Ordinal);
    }

    public static bool IsParentActivationTokenType(string? type) =>
        string.Equals(type, ActivationTokenConstants.TokenTypeValue, StringComparison.Ordinal);

    /// <summary>
    /// Exige <c>parent_activation</c> ; refuse explicitement <c>school_establishment</c>.
    /// </summary>
    public static void EnsureParentActivationTokenType(JwtSecurityToken jwt)
    {
        var type = ReadTokenType(jwt);
        if (string.Equals(type, SchoolEstablishmentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(RejectedEstablishmentMessage);
        }

        if (!IsParentActivationTokenType(type))
        {
            throw new InvalidOperationException(InvalidTypeMessage);
        }
    }

    /// <summary>Rejet précoce sans validation HMAC (controllers / Bootstrap).</summary>
    public static void EnsureNotSchoolEstablishmentToken(string? jwt)
    {
        if (IsSchoolEstablishmentToken(jwt))
        {
            throw new InvalidOperationException(RejectedEstablishmentMessage);
        }
    }
}
