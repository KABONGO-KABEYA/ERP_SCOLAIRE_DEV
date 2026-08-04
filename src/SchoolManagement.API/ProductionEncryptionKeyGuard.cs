using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SchoolManagement.Application.Configuration.Encryption;

/// <summary>
/// En Cloud / Production, la clé AES <c>ERP_CONFIG_ENCRYPTION_KEY</c> est obligatoire
/// (Linux/Docker et rôle Cloud). Le développement conserve une clé de secours locale.
/// </summary>
public static class ProductionEncryptionKeyGuard
{
    public const string EnvironmentVariableName = "ERP_CONFIG_ENCRYPTION_KEY";

    internal const string DevelopmentFallbackKey = AesConfigurationEncryptionService.DevFallbackKey;

    public static void EnsureConfigured(IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var role = configuration["Deployment:Role"]
                   ?? Environment.GetEnvironmentVariable("Deployment__Role")
                   ?? "Local";
        var isCloud = role.Equals("Cloud", StringComparison.OrdinalIgnoreCase);
        var isProduction = environment.IsProduction();

        if (!isProduction && !isCloud)
        {
            return;
        }

        // API locale Windows Production : chiffrement identité via DPAPI, pas AES.
        if (OperatingSystem.IsWindows() && !isCloud)
        {
            return;
        }

        var raw = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Variable d'environnement {EnvironmentVariableName} obligatoire en Cloud / Production " +
                $"(environnement={environment.EnvironmentName}, rôle={role}). " +
                "Définissez une clé secrète longue et aléatoire ; ne jamais utiliser la clé de développement.");
        }

        if (raw.Trim().Equals(AesConfigurationEncryptionService.DevFallbackKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} ne peut pas être la clé de développement par défaut en Cloud / Production.");
        }

        if (raw.Trim().Length < 16)
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} est trop courte (minimum 16 caractères recommandé).");
        }
    }
}
