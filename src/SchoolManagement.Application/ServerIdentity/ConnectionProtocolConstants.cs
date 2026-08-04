namespace SchoolManagement.Application.ServerIdentity;

/// <summary>Version du protocole connexion / discovery / health (indépendante du build applicatif).</summary>
public static class ConnectionProtocolConstants
{
    public const int ProtocolVersion = 2;

    /// <summary>Contrat REST global exposé dans /api/health.</summary>
    public const string ApiVersion = "1.0";

    public const string AppConfigurationLicenseIdKey = "Server.LicenseId";
}
