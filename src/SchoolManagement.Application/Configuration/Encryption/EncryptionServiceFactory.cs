namespace SchoolManagement.Application.Configuration.Encryption;

/// <summary>Choisit DPAPI (Windows) ou AES (Linux/Docker).</summary>
public static class EncryptionServiceFactory
{
    public static IEncryptionService Create() =>
        OperatingSystem.IsWindows()
            ? new EncryptionService()
            : new AesConfigurationEncryptionService();
}
