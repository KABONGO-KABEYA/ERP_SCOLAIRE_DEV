namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

public static class UpdateAgentCredentialStatuses
{
    public const string Active = "Active";
    public const string Revoked = "Revoked";
}

public static class UpdateAgentTokenConstants
{
    public const string TokenTypeClaim = "token_type";
    public const string TokenTypeValue = "update_agent";
    public const string SchoolIdClaim = "school_id";
    public const string ServerInstanceIdClaim = "server_instance_id";
    public const string Audience = "erp-scolaire-update-agent";
    public const string Issuer = "https://bootstrap.erp-scolaire.com";

    /// <summary>Longueur minimale UTF-8 de <c>Bootstrap:AgentJwtSigningKey</c> (HMAC-SHA256).</summary>
    public const int MinSigningKeyUtf8Bytes = 32;
}
