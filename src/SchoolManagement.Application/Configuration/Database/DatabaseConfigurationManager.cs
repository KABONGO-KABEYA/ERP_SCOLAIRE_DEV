using SchoolManagement.Application.Configuration.Encryption;

namespace SchoolManagement.Application.Configuration.Database;

/// <summary>
/// Point central de lecture/écriture de ServeurDonnees.txt.
/// Ne contient aucune logique d'interface graphique.
/// </summary>
public sealed class DatabaseConfigurationManager
{
    public const string FileName = "ServeurDonnees.txt";

    private const string DefaultHeader = """
        #######################################################
        # ERP SCOLAIRE RDC
        # Configuration SQL Server
        #######################################################
        """;

    private readonly string _applicationDirectory;
    private readonly IEncryptionService _encryptionService;

    public DatabaseConfigurationManager(string applicationDirectory, IEncryptionService encryptionService)
    {
        _applicationDirectory = applicationDirectory;
        _encryptionService = encryptionService;
    }

    public string ConfigurationFilePath => Path.Combine(_applicationDirectory, FileName);

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

    public DatabaseConfiguration LoadConfiguration()
    {
        EnsureDefaultFileExists();
        var content = File.ReadAllText(ConfigurationFilePath);
        var values = TextConfigurationFileParser.Parse(content);
        return MapToConfiguration(values, decryptPassword: true);
    }

    public DatabaseConfiguration LoadConfigurationWithoutPassword()
    {
        EnsureDefaultFileExists();
        var content = File.ReadAllText(ConfigurationFilePath);
        var values = TextConfigurationFileParser.Parse(content);
        return MapToConfiguration(values, decryptPassword: false);
    }

    public void SaveConfiguration(DatabaseConfiguration configuration, string plainPassword)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var validation = Validate(configuration, plainPassword);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, validation.FieldErrors.Values));
        }

        var encryptedPassword = _encryptionService.Encrypt(plainPassword);
        var values = CreateValuesFromConfiguration(configuration, encryptedPassword);
        Directory.CreateDirectory(_applicationDirectory);
        File.WriteAllText(ConfigurationFilePath, BuildFileContent(values));
    }

    public DatabaseConfigurationValidationResult Validate(DatabaseConfiguration configuration) =>
        Validate(configuration, configuration.MotDePasse);

    public DatabaseConfigurationValidationResult Validate(DatabaseConfiguration configuration, string plainPassword)
    {
        var result = new DatabaseConfigurationValidationResult();

        if (string.IsNullOrWhiteSpace(configuration.Serveur))
        {
            result.AddError(nameof(DatabaseConfiguration.Serveur), "Le serveur est obligatoire.");
        }

        if (configuration.Port is < 1 or > 65535)
        {
            result.AddError(nameof(DatabaseConfiguration.Port), "Le port doit être un nombre entre 1 et 65535.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Base))
        {
            result.AddError(nameof(DatabaseConfiguration.Base), "La base de données est obligatoire.");
        }

        if (configuration.Authentification == DatabaseAuthenticationMode.SqlServer)
        {
            if (string.IsNullOrWhiteSpace(configuration.Utilisateur))
            {
                result.AddError(nameof(DatabaseConfiguration.Utilisateur), "L'utilisateur SQL est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(plainPassword))
            {
                result.AddError("MotDePasse", "Le mot de passe SQL est obligatoire.");
            }
        }

        return result;
    }

    public DatabaseConfiguration CreateDefaultConfiguration() =>
        MapToConfiguration(CreateDefaultValues(string.Empty), decryptPassword: false);

    private DatabaseConfiguration MapToConfiguration(IReadOnlyDictionary<string, string> values, bool decryptPassword)
    {
        var configuration = new DatabaseConfiguration
        {
            Serveur = GetValue(values, "SERVEUR"),
            Base = GetValue(values, "BASE"),
            Utilisateur = GetValue(values, "UTILISATEUR"),
            Authentification = ParseAuthentication(GetValue(values, "AUTHENTIFICATION"))
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
            ["SERVEUR"] = "localhost",
            ["PORT"] = "1433",
            ["BASE"] = "SchoolManagementRDC",
            ["AUTHENTIFICATION"] = "SQL",
            ["UTILISATEUR"] = "sa",
            ["MOTDEPASSE"] = encryptedPassword
        };

    private static Dictionary<string, string> CreateValuesFromConfiguration(
        DatabaseConfiguration configuration,
        string encryptedPassword) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
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

    private static DatabaseAuthenticationMode ParseAuthentication(string value) =>
        value.Trim().Equals("WINDOWS", StringComparison.OrdinalIgnoreCase)
            ? DatabaseAuthenticationMode.Windows
            : DatabaseAuthenticationMode.SqlServer;

    private static string FormatAuthentication(DatabaseAuthenticationMode mode) =>
        mode == DatabaseAuthenticationMode.Windows ? "WINDOWS" : "SQL";
}
