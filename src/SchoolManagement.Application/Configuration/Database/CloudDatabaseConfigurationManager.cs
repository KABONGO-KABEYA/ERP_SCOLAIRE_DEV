using SchoolManagement.Application.Configuration.Encryption;

namespace SchoolManagement.Application.Configuration.Database;

/// <summary>
/// Lecture / écriture de ServeurDonneesCloud.txt (SQL distant, mot de passe DPAPI).
/// </summary>
public sealed class CloudDatabaseConfigurationManager
{
    public const string FileName = "ServeurDonneesCloud.txt";

    private const string DefaultHeader = """
        #######################################################
        # ERP SCOLAIRE RDC
        # Configuration SQL Server DISTANT (cloud)
        # Sync automatique locale → cloud dès qu'Internet est dispo
        # Ne jamais committer ce fichier (mot de passe chiffré machine)
        #######################################################
        """;

    private readonly string _applicationDirectory;
    private readonly IEncryptionService _encryptionService;

    public CloudDatabaseConfigurationManager(string applicationDirectory, IEncryptionService encryptionService)
    {
        _applicationDirectory = applicationDirectory;
        _encryptionService = encryptionService;
    }

    public string ConfigurationFilePath => Path.Combine(_applicationDirectory, FileName);

    public bool FileExists => File.Exists(ConfigurationFilePath);

    public void EnsureDefaultFileExists()
    {
        if (File.Exists(ConfigurationFilePath))
        {
            return;
        }

        Directory.CreateDirectory(_applicationDirectory);
        var defaults = CreateDefaultValues(encryptedPassword: string.Empty);
        File.WriteAllText(ConfigurationFilePath, BuildFileContent(defaults));
    }

    /// <summary>
    /// Crée ou met à jour la config cloud avec un mot de passe en clair (chiffré immédiatement).
    /// </summary>
    public void SaveConfiguration(CloudDatabaseConfiguration configuration, string plainPassword)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var validation = Validate(configuration, plainPassword);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, validation.FieldErrors.Values));
        }

        var encryptedPassword = configuration.Authentification == DatabaseAuthenticationMode.Windows
            ? string.Empty
            : _encryptionService.Encrypt(plainPassword);

        var values = CreateValuesFromConfiguration(configuration, encryptedPassword);
        Directory.CreateDirectory(_applicationDirectory);
        File.WriteAllText(ConfigurationFilePath, BuildFileContent(values));
    }

    public CloudDatabaseConfiguration LoadConfiguration()
    {
        if (!File.Exists(ConfigurationFilePath))
        {
            return CreateDefaultConfiguration();
        }

        var content = File.ReadAllText(ConfigurationFilePath);
        var values = TextConfigurationFileParser.Parse(content);
        return MapToConfiguration(values, decryptPassword: true);
    }

    public CloudDatabaseConfiguration LoadConfigurationWithoutPassword()
    {
        if (!File.Exists(ConfigurationFilePath))
        {
            return CreateDefaultConfiguration();
        }

        var content = File.ReadAllText(ConfigurationFilePath);
        var values = TextConfigurationFileParser.Parse(content);
        return MapToConfiguration(values, decryptPassword: false);
    }

    public DatabaseConfigurationValidationResult Validate(
        CloudDatabaseConfiguration configuration,
        string plainPassword)
    {
        var result = new DatabaseConfigurationValidationResult();

        if (!configuration.Actif)
        {
            return result;
        }

        if (string.IsNullOrWhiteSpace(configuration.Serveur))
        {
            result.AddError(nameof(CloudDatabaseConfiguration.Serveur), "Le serveur cloud est obligatoire.");
        }

        if (configuration.Port is < 1 or > 65535)
        {
            result.AddError(nameof(CloudDatabaseConfiguration.Port), "Le port doit être entre 1 et 65535.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Base))
        {
            result.AddError(nameof(CloudDatabaseConfiguration.Base), "La base cloud est obligatoire.");
        }

        if (configuration.IntervalleMinutes is < 1 or > 1440)
        {
            result.AddError(
                nameof(CloudDatabaseConfiguration.IntervalleMinutes),
                "L'intervalle doit être entre 1 et 1440 minutes.");
        }

        if (configuration.Authentification == DatabaseAuthenticationMode.SqlServer)
        {
            if (string.IsNullOrWhiteSpace(configuration.Utilisateur))
            {
                result.AddError(nameof(CloudDatabaseConfiguration.Utilisateur), "L'utilisateur SQL cloud est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(plainPassword))
            {
                result.AddError("MotDePasse", "Le mot de passe SQL cloud est obligatoire.");
            }
        }

        return result;
    }

    public CloudDatabaseConfiguration CreateDefaultConfiguration() =>
        MapToConfiguration(CreateDefaultValues(string.Empty), decryptPassword: false);

    private CloudDatabaseConfiguration MapToConfiguration(
        IReadOnlyDictionary<string, string> values,
        bool decryptPassword)
    {
        var configuration = new CloudDatabaseConfiguration
        {
            Serveur = GetValue(values, "SERVEUR"),
            Base = GetValue(values, "BASE"),
            Utilisateur = GetValue(values, "UTILISATEUR"),
            Authentification = ParseAuthentication(GetValue(values, "AUTHENTIFICATION")),
            Actif = ParseBool(GetValue(values, "ACTIF"), defaultValue: false),
            IntervalleMinutes = int.TryParse(GetValue(values, "INTERVALLE_MINUTES"), out var interval)
                ? Math.Clamp(interval, 1, 1440)
                : 5
        };

        configuration.Port = int.TryParse(GetValue(values, "PORT"), out var port) ? port : 1433;

        var encryptedPassword = GetValue(values, "MOTDEPASSE");
        if (decryptPassword && !string.IsNullOrWhiteSpace(encryptedPassword))
        {
            configuration.MotDePasse = _encryptionService.Decrypt(encryptedPassword);
        }

        return configuration;
    }

    private static Dictionary<string, string> CreateDefaultValues(string encryptedPassword) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ACTIF"] = "0",
            ["INTERVALLE_MINUTES"] = "5",
            ["SERVEUR"] = "161.97.105.22",
            ["PORT"] = "1433",
            ["BASE"] = "SchoolManagementRDC",
            ["AUTHENTIFICATION"] = "SQL",
            ["UTILISATEUR"] = "sa",
            ["MOTDEPASSE"] = encryptedPassword
        };

    private static Dictionary<string, string> CreateValuesFromConfiguration(
        CloudDatabaseConfiguration configuration,
        string encryptedPassword) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ACTIF"] = configuration.Actif ? "1" : "0",
            ["INTERVALLE_MINUTES"] = configuration.IntervalleMinutes.ToString(),
            ["SERVEUR"] = configuration.Serveur.Trim(),
            ["PORT"] = configuration.Port.ToString(),
            ["BASE"] = configuration.Base.Trim(),
            ["AUTHENTIFICATION"] = FormatAuthentication(configuration.Authentification),
            ["UTILISATEUR"] = configuration.Utilisateur.Trim(),
            ["MOTDEPASSE"] = encryptedPassword
        };

    private static string BuildFileContent(IReadOnlyDictionary<string, string> values) =>
        TextConfigurationFileParser.Serialize(values, DefaultHeader);

    private static string GetValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : string.Empty;

    private static bool ParseBool(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim() is "1" or "true" or "oui" or "yes";
    }

    private static DatabaseAuthenticationMode ParseAuthentication(string value) =>
        value.Trim().Equals("WINDOWS", StringComparison.OrdinalIgnoreCase)
            ? DatabaseAuthenticationMode.Windows
            : DatabaseAuthenticationMode.SqlServer;

    private static string FormatAuthentication(DatabaseAuthenticationMode mode) =>
        mode == DatabaseAuthenticationMode.Windows ? "WINDOWS" : "SQL";
}
