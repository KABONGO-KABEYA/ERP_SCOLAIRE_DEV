namespace SchoolManagement.Application.Configuration.FileStorage;

/// <summary>Lit et enregistre ServeurFichiers.txt (sans interface graphique).</summary>
public sealed class FileStorageConfigurationManager
{
    public const string FileName = "ServeurFichiers.txt";

    private const string DefaultHeader = """
        #######################################################
        # ERP SCOLAIRE RDC
        # Configuration des fichiers (dossiers élèves)
        #######################################################
        """;

    private readonly string _applicationDirectory;

    public FileStorageConfigurationManager(string applicationDirectory)
    {
        _applicationDirectory = applicationDirectory;
    }

    public string ConfigurationFilePath => Path.Combine(_applicationDirectory, FileName);

    public void EnsureDefaultFileExists()
    {
        if (File.Exists(ConfigurationFilePath))
        {
            return;
        }

        Directory.CreateDirectory(_applicationDirectory);
        var defaults = CreateDefaultValues();
        File.WriteAllText(ConfigurationFilePath, BuildFileContent(defaults));
    }

    public FileStorageConfiguration LoadConfiguration()
    {
        EnsureDefaultFileExists();
        var content = File.ReadAllText(ConfigurationFilePath);
        var values = TextConfigurationFileParser.Parse(content);
        return MapToConfiguration(values);
    }

    public void SaveConfiguration(FileStorageConfiguration configuration)
    {
        var validation = Validate(configuration);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, validation.FieldErrors.Values));
        }

        var values = CreateValuesFromConfiguration(configuration);
        Directory.CreateDirectory(_applicationDirectory);
        File.WriteAllText(ConfigurationFilePath, BuildFileContent(values));
    }

    public bool IsConfigured()
    {
        if (!File.Exists(ConfigurationFilePath))
        {
            return false;
        }

        var configuration = LoadConfiguration();
        return !string.IsNullOrWhiteSpace(configuration.Racine);
    }

    public FileStorageConfigurationValidationResult Validate(FileStorageConfiguration configuration)
    {
        var result = new FileStorageConfigurationValidationResult();

        if (string.IsNullOrWhiteSpace(configuration.Racine))
        {
            result.AddError(nameof(FileStorageConfiguration.Racine), "Le chemin du dossier partagé est obligatoire.");
            return result;
        }

        var formatError = ValidatePathFormat(configuration.Racine);
        if (formatError is not null)
        {
            result.AddError(nameof(FileStorageConfiguration.Racine), formatError);
        }

        return result;
    }

    public static string? ValidatePathFormat(string racine)
    {
        var trimmed = racine.Trim();

        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal))
        {
            if (trimmed.Length < 4 || trimmed.IndexOf('\\', 3) < 0)
            {
                return "Le chemin UNC doit être de la forme \\\\SERVEUR\\Partage\\Dossier_Elève.";
            }

            return null;
        }

        if (Path.IsPathRooted(trimmed))
        {
            var root = Path.GetPathRoot(trimmed);
            if (string.IsNullOrWhiteSpace(root) || root.Length < 3)
            {
                return "Le chemin absolu est invalide.";
            }

            return null;
        }

        return "Indiquez un chemin UNC (\\\\serveur\\partage\\Dossier_Elève) ou un chemin absolu (D:\\...). Les chemins relatifs ne sont pas autorisés.";
    }

    public string GetAbsoluteRootPath()
    {
        var configuration = LoadConfiguration();
        return ResolveAbsolutePath(configuration.Racine, _applicationDirectory);
    }

    public static string ResolveAbsolutePath(string racine, string applicationDirectory)
    {
        var trimmed = racine.Trim().TrimEnd('\\', '/');

        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return Path.GetFullPath(Path.Combine(applicationDirectory, trimmed));
    }

    public FileStorageConfiguration CreateDefaultConfiguration() =>
        MapToConfiguration(CreateDefaultValues());

    private static FileStorageConfiguration MapToConfiguration(IReadOnlyDictionary<string, string> values) =>
        new()
        {
            Racine = values.TryGetValue("RACINE", out var racine) ? racine : string.Empty
        };

    private static Dictionary<string, string> CreateDefaultValues() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RACINE"] = string.Empty
        };

    private static Dictionary<string, string> CreateValuesFromConfiguration(FileStorageConfiguration configuration) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RACINE"] = configuration.Racine.Trim()
        };

    private static string BuildFileContent(IReadOnlyDictionary<string, string> values) =>
        TextConfigurationFileParser.Serialize(values, DefaultHeader);
}
