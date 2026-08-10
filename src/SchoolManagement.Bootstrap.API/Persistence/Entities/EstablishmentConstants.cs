namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

/// <summary>Statut d'un credential d'établissement dans le registre Bootstrap.</summary>
public static class EstablishmentCredentialStatuses
{
    public const string Active = "Active";
    public const string Revoked = "Revoked";
}

/// <summary>Type de token QR établissement (contrat JWT <c>token_type</c>).</summary>
public static class EstablishmentTokenTypes
{
    public const string SchoolEstablishment = "school_establishment";
}

/// <summary>Statut session establish start→complete.</summary>
public static class EstablishmentSessionStatuses
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Expired = "Expired";
}
