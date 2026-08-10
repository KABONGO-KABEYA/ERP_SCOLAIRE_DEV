using SchoolManagement.Bootstrap.API.Persistence.Entities;

namespace SchoolManagement.Bootstrap.API.Establishment;

public static class EstablishmentTokenConstants
{
    public const string TokenTypeClaim = "token_type";

    public const string TokenTypeValue = EstablishmentTokenTypes.SchoolEstablishment;

    public const string SchoolIdClaim = "school_id";

    public const string VersionClaim = "ver";

    public const string Audience = "erp-scolaire-mobile-establish";

    public const string BootstrapIssuer = "https://bootstrap.erp-scolaire.com";

    public const string BindingKindExtensionKey = "bindingKind";

    public const string BindingKindExtensionValue = EstablishmentTokenTypes.SchoolEstablishment;

    public const string CredentialVersionExtensionKey = "establishmentCredentialVersion";

    public static string SchoolIssuer(Guid schoolId) => $"school:{schoolId:D}";
}

/// <summary>Erreur métier establishment avec status HTTP cible.</summary>
public sealed class EstablishmentException : Exception
{
    public EstablishmentException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
